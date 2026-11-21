"""Play a match over the real TCP protocol, using only the Python standard library."""
import argparse
import json
import socket
from contextlib import ExitStack


def send(stream, **command):
    stream.write((json.dumps(command) + "\n").encode())


def receive(stream):
    line = stream.readline()
    assert line, "Server closed the connection"
    return json.loads(line)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=7777)
    parser.add_argument("--players", type=int, choices=range(2, 6), default=5)
    args = parser.parse_args()
    with ExitStack() as stack:
        clients = {}

        def connect():
            sock = stack.enter_context(socket.create_connection((args.host, args.port), timeout=5))
            return stack.enter_context(sock.makefile("rwb", buffering=0))

        for i in range(args.players):
            stream = connect()
            send(stream, type="hello", protocol=1, name=f"Участник {i+1}")
            fresh = receive(stream)
            assert fresh["type"] == "state", fresh
            for peer in clients.values():
                assert receive(peer)["state"] == fresh["state"]
            clients[fresh["me"]] = stream
        host = next(iter(clients))

        def snapshot():
            views = [receive(stream) for stream in clients.values()]
            state = views[0]["state"]
            assert all(v["type"] == "state" and v["state"] == state for v in views)
            return state

        send(clients[host], type="start")
        state = snapshot()
        if state.get("independentSpin"):
            send(clients[host], type="spin", revision=state["revision"])
            state = snapshot()
            assert state["spinActor"] == host and state["spinSeq"] == state["revision"]
            assert state["turn"] == host and not state.get("shot")
        outsider = connect()
        send(outsider, type="hello", protocol=1, name="Гость")
        assert receive(outsider)["type"] == "error"
        shots = 0
        while state["phase"] == "match":
            actor = state["turn"]
            other = next(i for i in clients if i != actor)
            send(clients[other], type="move", target=actor, revision=state["revision"])
            assert receive(clients[other])["type"] == "error"
            survivors = [p["id"] for p in state["players"] if p["alive"]]
            target = actor if shots % 3 == 0 else next(i for i in survivors if i != actor)
            send(clients[actor], type="move", revision=state["revision"], target=target, spin=shots == 0)
            state = snapshot()
            assert state["shot"]["target"] == target
            shots += 1
            assert shots <= 6 * (args.players - 1)
        assert sum(p["alive"] for p in state["players"]) == 1
        send(clients[host], type="lobby")
        assert snapshot()["phase"] == "lobby"
        print(f"{args.players} clients, {shots} synchronized actions, winner {state['winner']}, rematch OK")


if __name__ == "__main__":
    main()
