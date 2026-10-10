using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using BalanceGame.Interactions;

namespace BalancePuzzle.Tests
{
    /// <summary>
    /// Milestone 1.2 — ProximityAlignmentVisualizer verification.
    ///
    /// Fully synthetic rig (no Stage 1 scene): a camera, a base stone and a
    /// held stone, each marked with StoneState. The rig sits at x/z = 100 so
    /// the 1.2-unit proximity scan can never match leftover Stage 1 content.
    /// Holds are driven through the component's BeginHold/EndHold funnel — the
    /// same funnel the StoneDragger poll and StoneReleased event feed.
    /// </summary>
    public sealed class ProximityAlignmentVisualizerTest
    {
        private const float BaseX = 100f;
        private const float BaseZ = 100f;

        private readonly WaitForSeconds _settle = new WaitForSeconds(1f);
        private readonly WaitForSeconds _shortSettle = new WaitForSeconds(0.5f);

        private GameObject _root;
        private Camera _camera;
        private ProximityAlignmentVisualizer _visualizer;
        private GameObject _held;

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
            {
                Object.Destroy(_root);
                _root = null;
            }
            _camera = null;
            _visualizer = null;
            _held = null;
        }

        [UnityTest]
        public IEnumerator HeldStoneFarAwayKeepsDefaultFovAndHidesDots()
        {
            BuildRig();
            yield return null;

            Assert.IsFalse(_visualizer.DotsActive, "Dots must start inactive.");
            Assert.AreEqual(60f, _camera.fieldOfView, 0.01f, "FOV must start at 60°.");

            _visualizer.BeginHold(_held); // Held at (105, 1, 100): far from base.
            yield return _settle;

            Assert.AreEqual(60f, _camera.fieldOfView, 0.5f, "Far hold must keep FOV at 60°.");
            Assert.IsFalse(_visualizer.DotsActive, "Far hold must keep dots inactive.");
        }

        [UnityTest]
        public IEnumerator NearStoneZoomsFovAndShowsMisalignedDots()
        {
            BuildRig();
            yield return null;

            _visualizer.BeginHold(_held);
            // XZ offset 0.35 (> 0.08): near enough to zoom, visibly misaligned.
            _held.transform.position = new Vector3(BaseX + 0.35f, 1.5f, BaseZ);
            yield return _settle;

            Assert.AreEqual(42f, _camera.fieldOfView, 1f, "Near hold must zoom FOV toward 42°.");
            Assert.IsTrue(_visualizer.DotsActive, "Near hold must activate both dots.");
            Assert.AreEqual(ProximityAlignmentVisualizer.MisalignedColor, DotColor(),
                "Offset 0.35 must show the blue misaligned state.");
            Assert.AreEqual(1.25f, UpperDotY(), 0.02f,
                "Upper dot must track the held stone's lowest bound point.");
            Assert.AreEqual(1f, LowerDotY(), 0.02f,
                "Lower dot must track the base stone's highest bound point.");
        }

        [UnityTest]
        public IEnumerator AlignedHoldShowsGoldDots()
        {
            BuildRig();
            yield return null;

            _visualizer.BeginHold(_held);
            // XZ offset ~0.036 (<= 0.08): aligned.
            _held.transform.position = new Vector3(BaseX + 0.03f, 1.5f, BaseZ - 0.02f);
            yield return _settle;

            Assert.IsTrue(_visualizer.DotsActive, "Aligned hold must keep dots active.");
            Assert.LessOrEqual(_visualizer.AlignmentOffset, 0.08f, "Offset must read aligned.");
            Assert.AreEqual(ProximityAlignmentVisualizer.AlignedColor, DotColor(),
                "Aligned offset must show the gold state.");
        }

        [UnityTest]
        public IEnumerator ReleaseHidesDotsAndRestoresDefaultFov()
        {
            BuildRig();
            yield return null;

            _visualizer.BeginHold(_held);
            _held.transform.position = new Vector3(BaseX + 0.35f, 1.5f, BaseZ);
            yield return _settle;
            Assert.IsTrue(_visualizer.DotsActive, "Precondition: dots must be active.");

            _visualizer.EndHold();
            yield return _settle;

            Assert.IsFalse(_visualizer.DotsActive, "Release must deactivate dots.");
            Assert.AreEqual(60f, _camera.fieldOfView, 1f, "Release must restore FOV toward 60°.");
        }

        // ============================ helpers ============================

        private void BuildRig()
        {
            _root = new GameObject("ProximityTestRig");

            var camGo = new GameObject("TestCamera");
            camGo.transform.SetParent(_root.transform, false);
            _camera = camGo.AddComponent<Camera>();
            _camera.enabled = false; // Logic only: FOV is a plain property.
            _camera.fieldOfView = 60f;

            // Base stone: unit cube sitting on y=0, top face at y=1.
            BuildStone("BaseStone", new Vector3(BaseX, 0.5f, BaseZ), Vector3.one);

            // Held stone: half-size cube, starts far away, moves per test.
            _held = BuildStone("HeldStone", new Vector3(BaseX + 5f, 1f, BaseZ),
                Vector3.one * 0.5f);

            var vizGo = new GameObject("ProximityVisualizer");
            vizGo.transform.SetParent(_root.transform, false);
            _visualizer = vizGo.AddComponent<ProximityAlignmentVisualizer>();
            _visualizer.SetCamera(_camera);
        }

        private GameObject BuildStone(string name, Vector3 position, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform, false);
            go.transform.position = position;
            var box = go.AddComponent<BoxCollider>();
            box.size = size;
            var body = go.AddComponent<Rigidbody>();
            body.isKinematic = true; // Frozen: tests pose stones via Transform.
            go.AddComponent<StoneState>(); // Stone identity marker; quiet without LevelReset.
            return go;
        }

        private Color DotColor()
        {
            var renderer = _visualizer.GetComponentInChildren<Renderer>();
            return renderer.sharedMaterial.color; // Shared: .material would clone it.
        }

        private float UpperDotY()
        {
            return _visualizer.transform.Find("AlignmentUpperDot").position.y;
        }

        private float LowerDotY()
        {
            return _visualizer.transform.Find("AlignmentLowerDot").position.y;
        }
    }
}
