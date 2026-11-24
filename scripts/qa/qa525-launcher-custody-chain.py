"""Pure custody graph check. No filesystem access or deployment authority.
Inputs must already be authenticated by generation-specific journal validators.
"""
import re

HEX = re.compile(r"[0-9a-f]{64}")

def _hex(value):
    if type(value) is not str or HEX.fullmatch(value) is None:
        raise ValueError("CUSTODY_DIGEST_INVALID")
    return value

def verify_chain(*, original, live, edges):
    """Require a single complete path, with no replay, fork or orphan.
    Edges are (nonce, old_hex, new_hex) from authenticated receipts only.
    """
    original, live = _hex(original), _hex(live)
    if type(edges) not in (list, tuple) or len(edges) > 1000:
        raise ValueError("CUSTODY_EDGES_INVALID")
    outgoing, incoming, nonces = {}, set(), set()
    for edge in edges:
        if type(edge) not in (tuple, list) or len(edge) != 3:
            raise ValueError("CUSTODY_EDGE_INVALID")
        nonce, old, new = map(_hex, edge)
        if nonce in nonces or old in outgoing or new in incoming or old == new:
            raise ValueError("CUSTODY_AMBIGUOUS")
        nonces.add(nonce)
        outgoing[old] = new
        incoming.add(new)
    current, visited = original, set()
    while current in outgoing:
        if current in visited:
            raise ValueError("CUSTODY_CYCLE")
        visited.add(current)
        current = outgoing[current]
    if current != live or len(visited) != len(outgoing):
        raise ValueError("CUSTODY_DISCONNECTED")
    return tuple(sorted(nonces))
