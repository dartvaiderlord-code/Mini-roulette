package main

import (
	"math/rand"
	"testing"
)

func match(t *testing.T, n int) *Room {
	t.Helper()
	g := NewRoom(rand.New(rand.NewSource(93)))
	for i := 0; i < n; i++ {
		if _, e := g.join("гость", false); e != "" {
			t.Fatal(e)
		}
	}
	if e := g.start(g.Host); e != "" {
		t.Fatal(e)
	}
	return g
}
func TestRingAdvancesAndSpinRepositions(t *testing.T) {
	rng := rand.New(rand.NewSource(83))
	r := &Ring{slots: make([]*Charge, 6)}
	for k := 0; k < 20; k++ {
		r.Refill(rng, Charge{"burst"})
		hits := 0
		for i := 0; i < 6; i++ {
			if c, h := r.Activate(rng, false); h {
				hits++
				if c.Effect != "burst" {
					t.Fatal(c)
				}
			}
		}
		if hits != 1 || r.cursor != 0 {
			t.Fatalf("cycle hits %d cursor %d", hits, r.cursor)
		}
	}
	r.Refill(rng, Charge{"burst"})
	reference := rand.New(rand.NewSource(94))
	expected := reference.Intn(6)
	r.Activate(rand.New(rand.NewSource(94)), true)
	if r.cursor != (expected+1)%6 {
		t.Fatal("spin did not select slot")
	}
}
func TestRulesSpectatorAndRematch(t *testing.T) {
	g := match(t, 3)
	g.device.Magazine = &Ring{slots: []*Charge{nil, nil, {Effect: "burst"}}}
	a, b, c := g.Players[0].ID, g.Players[1].ID, g.Players[2].ID
	if e := g.move(a, a, g.Revision, false); e != "" || g.Turn != a {
		t.Fatal("self blank", e)
	}
	if e := g.move(a, c, g.Revision, false); e != "" || g.Turn != b {
		t.Fatal("other blank", e)
	}
	before := g.Revision
	if g.move(b, c, before-1, false) == "" || g.move(a, b, before, false) == "" || g.move(b, -8, before, false) == "" || g.Revision != before {
		t.Fatal("invalid move mutated state")
	}
	if e := g.move(b, c, g.Revision, false); e != "" || g.Turn != a || g.Round != 2 || g.byID(c).Alive || !g.Shot.Hit {
		t.Fatal("hit", e, g.State)
	}
	if g.move(c, a, g.Revision, false) == "" || g.move(a, c, g.Revision, false) == "" {
		t.Fatal("spectator involved in move")
	}
	g.device.Magazine = &Ring{slots: []*Charge{{Effect: "burst"}}}
	if e := g.move(a, a, g.Revision, false); e != "" || g.Phase != "end" || g.Winner != b {
		t.Fatal("finish", e)
	}
	if g.returnLobby(b) == "" || g.returnLobby(a) != "" || g.start(a) != "" || g.survivors() != 3 || g.Round != 1 {
		t.Fatal("rematch")
	}
}
func TestLobbyAndDeparture(t *testing.T) {
	g := match(t, 5)
	a, b := g.Players[0].ID, g.Players[1].ID
	if p, _ := g.join("late", false); p != nil {
		t.Fatal("late join")
	}
	g.leave(a)
	if g.Host != b || g.Turn != b || g.byID(a).Alive {
		t.Fatal("host departure")
	}
	for _, p := range g.Players[2:] {
		g.leave(p.ID)
	}
	if g.Winner != b {
		t.Fatal("remaining player should win")
	}
	if g.returnLobby(b) != "" || len(g.Players) != 1 || g.start(b) == "" {
		t.Fatal("room reset")
	}
	for len(g.Players) < 5 {
		g.join("вход", false)
	}
	if p, _ := g.join("шестой", false); p != nil {
		t.Fatal("sixth player admitted")
	}
}
func TestSizeAndBots(t *testing.T) {
	for n := 2; n <= 5; n++ {
		g := match(t, n)
		for _, p := range g.Players {
			p.Bot = true
		}
		for i := 0; i < 1024 && g.Phase == "match"; i++ {
			g.botTurn()
		}
		if g.Phase != "end" || g.survivors() != 1 || g.Round != n-1 {
			t.Fatal("bot match", n, g.State)
		}
	}
}
