package main

import (
	"math/rand"
	"strings"
	"unicode"
)

// A device deals abstract game events. The room never serializes its private slots.
type Charge struct{ Effect string }
type Magazine interface {
	Refill(*rand.Rand, Charge)
	Spin(*rand.Rand)
	Activate(*rand.Rand, bool) (Charge, bool)
}
type Ring struct {
	slots  []*Charge
	cursor int
}

func (r *Ring) Refill(rng *rand.Rand, charge Charge) {
	clear(r.slots)
	r.cursor = 0
	r.slots[rng.Intn(len(r.slots))] = &charge
}
func (r *Ring) Spin(rng *rand.Rand) { r.cursor = rng.Intn(len(r.slots)) }
func (r *Ring) Activate(rng *rand.Rand, spin bool) (Charge, bool) {
	if spin {
		r.Spin(rng)
	}
	i := r.cursor
	r.cursor = (i + 1) % len(r.slots)
	if r.slots[i] == nil {
		return Charge{}, false
	}
	c := *r.slots[i]
	r.slots[i] = nil
	return c, true
}

type Device struct {
	Name     string
	Magazine Magazine
	Load     Charge
}

type Player struct {
	ID        int    `json:"id"`
	Name      string `json:"name"`
	Bot       bool   `json:"bot"`
	Alive     bool   `json:"alive"`
	Connected bool   `json:"connected"`
}
type Shot struct {
	Seq    int    `json:"seq"`
	Actor  int    `json:"actor"`
	Target int    `json:"target"`
	Spin   bool   `json:"spin"`
	Hit    bool   `json:"hit"`
	Seed   int    `json:"seed"`
	Effect string `json:"effect"`
}
type State struct {
	IndependentSpin bool      `json:"independentSpin"`
	SpinSeq         int       `json:"spinSeq"`
	SpinActor       int       `json:"spinActor"`
	Phase           string    `json:"phase"`
	Revision        int       `json:"revision"`
	Round           int       `json:"round"`
	Turn            int       `json:"turn"`
	Winner          int       `json:"winner"`
	Host            int       `json:"host"`
	Players         []*Player `json:"players"`
	Shot            *Shot     `json:"shot,omitempty"`
}
type Room struct {
	State
	rng    *rand.Rand
	device Device
	serial int
}

func NewRoom(rng *rand.Rand) *Room {
	return &Room{State: State{IndependentSpin: true, Phase: "lobby", Turn: -1, Winner: -1, Host: -1}, rng: rng, device: Device{"Карусель", &Ring{slots: make([]*Charge, 6)}, Charge{"burst"}}}
}
func (g *Room) byID(id int) *Player {
	for _, p := range g.Players {
		if p.ID == id {
			return p
		}
	}
	return nil
}
func (g *Room) join(name string, bot bool) (*Player, string) {
	if g.Phase != "lobby" {
		return nil, "Матч уже идёт"
	}
	if len(g.Players) >= 5 {
		return nil, "Все места заняты"
	}
	runes := []rune(strings.TrimSpace(strings.Map(func(r rune) rune {
		if unicode.IsControl(r) || r == '<' || r == '>' {
			return -1
		}
		return r
	}, name)))
	if len(runes) > 16 {
		runes = runes[:16]
	}
	name = strings.TrimSpace(string(runes))
	if name == "" {
		name = "Гость"
	}
	g.serial++
	p := &Player{g.serial, name, bot, true, true}
	g.Players = append(g.Players, p)
	if g.Host == -1 && !bot {
		g.Host = p.ID
	}
	g.Revision++
	return p, ""
}
func (g *Room) start(id int) string {
	if id != g.Host || g.Phase != "lobby" || len(g.Players) < 2 {
		return "Старт доступен ведущему при двух участниках"
	}
	g.Phase = "match"
	g.Round = 1
	g.Turn = g.Players[0].ID
	g.SpinSeq, g.SpinActor = 0, 0
	g.Shot = nil
	g.Winner = -1
	g.device.Magazine.Refill(g.rng, g.device.Load)
	g.Revision++
	return ""
}
func (g *Room) survivors() int {
	n := 0
	for _, p := range g.Players {
		if p.Alive {
			n++
		}
	}
	return n
}
func (g *Room) settle(actor int, pass bool) {
	if g.survivors() <= 1 {
		g.Phase = "end"
		g.Turn = -1
		g.Winner = -1
		for _, p := range g.Players {
			if p.Alive {
				g.Winner = p.ID
			}
		}
		return
	}
	if !pass {
		return
	}
	i := 0
	for i < len(g.Players) && g.Players[i].ID != actor {
		i++
	}
	for n := 1; n <= len(g.Players); n++ {
		p := g.Players[(i+n)%len(g.Players)]
		if p.Alive {
			g.Turn = p.ID
			return
		}
	}
}
func (g *Room) spin(id, revision int) string {
	if g.Phase != "match" || g.Revision != revision || g.Turn != id {
		return "Ход уже изменился"
	}
	g.device.Magazine.Spin(g.rng)
	g.Revision++
	g.SpinSeq = g.Revision
	g.SpinActor = id
	return ""
}
func (g *Room) move(id, target, revision int, spin bool) string {
	if g.Phase != "match" || revision != g.Revision || id != g.Turn {
		return "Ход уже изменился"
	}
	p := g.byID(target)
	if p == nil || !p.Alive {
		return "Выберите участника в игре"
	}
	charge, hit := g.device.Magazine.Activate(g.rng, spin)
	g.Revision++
	g.Shot = &Shot{g.Revision, id, target, spin, hit, int(g.rng.Int31n(1 << 30)), charge.Effect}
	if hit {
		p.Alive = false
	}
	g.settle(id, hit || target != id)
	if hit && g.Phase == "match" {
		g.Round++
		g.device.Magazine.Refill(g.rng, g.device.Load)
	}
	return ""
}
func (g *Room) botTurn() {
	p := g.byID(g.Turn)
	if g.Phase != "match" || p == nil || !p.Bot {
		return
	}
	var targets []int
	for _, q := range g.Players {
		if q.Alive {
			targets = append(targets, q.ID)
		}
	}
	g.move(p.ID, targets[g.rng.Intn(len(targets))], g.Revision, g.rng.Intn(2) == 0)
}
func (g *Room) returnLobby(id int) string {
	if g.Phase != "end" || id != g.Host {
		return "В лобби может вернуть ведущий после матча"
	}
	seats := g.Players[:0]
	for _, p := range g.Players {
		if p.Connected {
			p.Alive = true
			seats = append(seats, p)
		}
	}
	g.Players = seats
	g.Phase = "lobby"
	g.Turn = -1
	g.Winner = -1
	g.Round = 0
	g.SpinSeq, g.SpinActor = 0, 0
	g.Shot = nil
	g.Revision++
	return ""
}
func (g *Room) leave(id int) {
	p := g.byID(id)
	if p == nil {
		return
	}
	p.Connected = false
	p.Alive = false
	g.Revision++
	if g.Host == id {
		g.Host = -1
		for _, q := range g.Players {
			if q.Connected && !q.Bot {
				g.Host = q.ID
				break
			}
		}
	}
	if g.Phase == "lobby" {
		for i, q := range g.Players {
			if q == p {
				g.Players = append(g.Players[:i], g.Players[i+1:]...)
				break
			}
		}
	} else if g.Phase == "match" {
		g.settle(id, id == g.Turn)
	}
}
