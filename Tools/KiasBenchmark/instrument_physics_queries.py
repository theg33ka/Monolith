"""Instrument isolated engine worktrees only, without changing production physics."""
import argparse
import hashlib
import json
from pathlib import Path

SIGNATURES = [
    "public void Query(Box2 aabb, uint maskBits, TreeQueryCallback callback)",
    "public void Query<TState>(ref TState state, QueryCallback<TState> callback, in Box2 aabb)",
    "public void Query<TState>(ref TState state, QueryCallback<TState> callback, Vector2 point)",
    "public void FastQuery(ref Box2 aabb, FastQueryCallback callback)",
    "public void RayCast<TState>(ref TState state, RayQueryCallback<TState> callback, in Ray input)",
]


def instrument(engine):
    path = engine / "Robust.Shared/Physics/B2DynamicTree.cs"
    code = path.read_text(encoding="utf-8-sig")
    if "DiagnosticQueryCount" not in code:
        anchor = "        #region Queries"
        assert code.count(anchor) == 1, "Unexpected engine query layout"
        code = code.replace(anchor,
            "        private static long _diagnosticQueryCount;\n"
            "        public static long DiagnosticQueryCount => System.Threading.Interlocked.Read(ref _diagnosticQueryCount);\n\n" + anchor)
        for signature in SIGNATURES:
            anchor = "        " + signature + "\n        {"
            assert code.count(anchor) == 1, signature
            code = code.replace(anchor, anchor + "\n            System.Threading.Interlocked.Increment(ref _diagnosticQueryCount);")
        path.write_text(code, encoding="utf-8")
    return dict(path=str(path), sha256=hashlib.sha256(path.read_bytes()).hexdigest(),
                scope="One count per B2DynamicTree<T> traversal, wrappers excluded. FixtureProxy specialization includes collision broadphase and physics query traversals.",
                instrumentedEntryPoints=SIGNATURES)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("engine", type=Path)
    args = parser.parse_args()
    print(json.dumps(instrument(args.engine), indent=2))
