package main

import (
	"bufio"
	"crypto/rand"
	"encoding/binary"
	"encoding/json"
	"flag"
	"fmt"
	"io"
	"log"
	mathrand "math/rand"
	"net"
	"os"
	"sync"
	"time"
)

const protocol = 1

type command struct {
	Type     string `json:"type"`
	Protocol int    `json:"protocol"`
	Name     string `json:"name"`
	Target   int    `json:"target"`
	Spin     bool   `json:"spin"`
	Revision int    `json:"revision"`
}
type reply struct {
	Type    string `json:"type"`
	Me      int    `json:"me"`
	State   *State `json:"state,omitempty"`
	Message string `json:"message,omitempty"`
}
type client struct {
	conn net.Conn
	out  chan []byte
	id   int
}
type table struct {
	sync.Mutex
	game   *Room
	peers  map[*client]bool
	bots   int
	turnAt time.Time
}

func (t *table) send(c *client, r reply) {
	b, _ := json.Marshal(r)
	select {
	case c.out <- append(b, '\n'):
	default:
		c.conn.Close()
	}
}
func (t *table) broadcast() {
	for c := range t.peers {
		t.send(c, reply{Type: "state", Me: c.id, State: &t.game.State})
	}
	t.turnAt = time.Now().Add(1500 * time.Millisecond)
}
func (t *table) handle(c *client, q command) {
	t.Lock()
	defer t.Unlock()
	g := t.game
	err := ""
	if c.id == 0 {
		if q.Type != "hello" || q.Protocol != protocol {
			err = "Несовместимая версия клиента"
		} else {
			var p *Player
			p, err = g.join(q.Name, false)
			if err == "" {
				c.id = p.ID
				t.peers[c] = true
				if t.bots > 0 {
					names := []string{"Бруно", "Гром", "Бублик", "Феликс"}
					for i := 0; i < t.bots; i++ {
						g.join(names[i], true)
					}
					g.start(c.id)
				}
			}
		}
	} else {
		switch q.Type {
		case "start":
			err = g.start(c.id)
		case "move":
			err = g.move(c.id, q.Target, q.Revision, q.Spin)
		case "lobby":
			err = g.returnLobby(c.id)
		default:
			err = "Неизвестная команда"
		}
	}
	if err != "" {
		t.send(c, reply{Type: "error", Message: err})
		return
	}
	t.broadcast()
}
func (t *table) serve(conn net.Conn) {
	c := &client{conn: conn, out: make(chan []byte, 32)}
	defer func() {
		conn.Close()
		t.Lock()
		defer t.Unlock()
		close(c.out)
		if !t.peers[c] {
			return
		}
		delete(t.peers, c)
		t.game.leave(c.id)
		if len(t.peers) == 0 {
			t.game = NewRoom(t.game.rng)
		} else {
			t.broadcast()
		}
	}()
	go func() {
		for b := range c.out {
			conn.SetWriteDeadline(time.Now().Add(5 * time.Second))
			if _, err := conn.Write(b); err != nil {
				conn.Close()
				return
			}
		}
	}()
	s := bufio.NewScanner(conn)
	s.Buffer(make([]byte, 1024), 4096)
	conn.SetReadDeadline(time.Now().Add(10 * time.Second))
	for s.Scan() {
		var q command
		if json.Unmarshal(s.Bytes(), &q) != nil {
			return
		}
		t.handle(c, q)
		if c.id != 0 {
			conn.SetReadDeadline(time.Time{})
		}
	}
}
func main() {
	addr := flag.String("listen", ":7777", "TCP address")
	bots := flag.Int("bots", 0, "local practice opponents (0–4)")
	flag.Parse()
	if *bots < 0 || *bots > 4 {
		log.Fatal("bots must be 0–4")
	}
	var seed int64
	if err := binary.Read(rand.Reader, binary.LittleEndian, &seed); err != nil {
		log.Fatal(err)
	}
	l, err := net.Listen("tcp", *addr)
	if err != nil {
		log.Fatal(err)
	}
	fmt.Println(l.Addr().String())
	t := &table{game: NewRoom(mathrand.New(mathrand.NewSource(seed))), peers: make(map[*client]bool), bots: *bots}
	if *bots > 0 {
		go func() {
			tick := time.NewTicker(150 * time.Millisecond)
			for range tick.C {
				t.Lock()
				if time.Now().After(t.turnAt) {
					before := t.game.Revision
					t.game.botTurn()
					if before != t.game.Revision {
						t.broadcast()
					}
				}
				t.Unlock()
			}
		}()
	}
	// A local Unity client owns this child. EOF also releases it if the client crashes.
	if os.Getenv("CAROUSEL_CHILD") == "1" {
		go func() { io.Copy(io.Discard, os.Stdin); os.Exit(0) }()
	}
	for {
		conn, e := l.Accept()
		if e != nil {
			log.Fatal(e)
		}
		go t.serve(conn)
	}
}
