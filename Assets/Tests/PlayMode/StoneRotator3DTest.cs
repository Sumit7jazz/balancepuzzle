using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using BalanceGame.Interactions;

namespace BalancePuzzle.Tests
{
    /// <summary>
    /// Milestone 1.3 — Object3DRotationManipulator verification.
    ///
    /// Synthetic rig (kinematic stone + camera, parked at x/z = 100, clear of
    /// Stage 1 leftovers). Deltas are fed through AddRotationDelta/AddRollDelta
    /// and rotation is read back from the Rigidbody, asserting angle, axis,
    /// camera-relativity, COM preservation and release behavior.
    /// </summary>
    public sealed class StoneRotator3DTest
    {
        private const float BaseX = 100f;
        private const float BaseZ = 100f;
        private static readonly Vector3 ConfiguredCom = new Vector3(0.2f, 0.1f, -0.1f);

        private readonly WaitForSeconds _settle = new WaitForSeconds(0.5f);

        private GameObject _root;
        private Camera _camera;
        private Object3DRotationManipulator _manipulator;
        private Rigidbody _body;

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
            {
                Object.Destroy(_root);
                _root = null;
            }
            _camera = null;
            _manipulator = null;
            _body = null;
        }

        [UnityTest]
        public IEnumerator ScreenDeltaInducesCameraRelativeAngularChange()
        {
            BuildRig();
            yield return null;

            Quaternion before = _body.rotation;
            _manipulator.AddRotationDelta(new Vector2(100f, 0f)); // 25° about camera up.
            yield return _settle;

            Assert.AreEqual(25f, Quaternion.Angle(before, _body.rotation), 1f,
                "100px yaw delta must rotate the stone ~25°.");
            AssertAxisParallel(_body.rotation * Quaternion.Inverse(before),
                _camera.transform.up, "Yaw must act about the camera up axis.");

            before = _body.rotation;
            _manipulator.AddRotationDelta(new Vector2(0f, 60f)); // 15° about camera right.
            yield return _settle;

            Assert.AreEqual(15f, Quaternion.Angle(before, _body.rotation), 1f,
                "60px pitch delta must rotate the stone ~15°.");
            AssertAxisParallel(_body.rotation * Quaternion.Inverse(before),
                _camera.transform.right, "Pitch must act about the camera right axis.");
        }

        [UnityTest]
        public IEnumerator OffsetCameraTransformsRotationToItsView()
        {
            BuildRig();
            // Swing the camera to a 45° azimuth, level: axes must follow the view.
            _camera.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
            yield return null;

            Quaternion before = _body.rotation;
            _manipulator.AddRotationDelta(new Vector2(100f, 0f));
            yield return _settle;

            Assert.AreEqual(25f, Quaternion.Angle(before, _body.rotation), 1f,
                "Yaw magnitude must not depend on camera azimuth.");
            AssertAxisParallel(_body.rotation * Quaternion.Inverse(before),
                _camera.transform.up,
                "Yaw must follow the offset camera's up axis.");
            Assert.Greater(Mathf.Abs(_camera.transform.right.x), 0.5f,
                "Precondition: camera right must differ from world X at 45°.");
        }

        [UnityTest]
        public IEnumerator RotationPreservesConfiguredCenterOfMass()
        {
            BuildRig();
            yield return null;

            Vector3 startPos = _body.position;
            Assert.AreEqual(ConfiguredCom, _body.centerOfMass,
                "Precondition: COM must hold the configured offset.");

            _manipulator.AddRotationDelta(new Vector2(80f, 40f));
            _manipulator.AddRollDelta(50f);
            yield return _settle;
            Assert.Greater(Quaternion.Angle(Quaternion.identity, RelativeSpin()), 5f,
                "Precondition: the stone must actually have rotated.");

            Assert.AreEqual(ConfiguredCom, _body.centerOfMass,
                "Local COM must be untouched by rotation.");
            Assert.AreEqual(0f, Vector3.Distance(startPos, _body.position), 1e-3f,
                "Rotation must not translate the stone.");
            Vector3 expectedWorldCom = _body.position + _body.rotation * ConfiguredCom;
            Assert.AreEqual(0f, Vector3.Distance(expectedWorldCom, _body.worldCenterOfMass),
                1e-3f, "World COM must ride the rotation about the local COM.");
        }

        [UnityTest]
        public IEnumerator ReleaseStopsRotationAccumulationCleanly()
        {
            BuildRig();
            yield return null;

            _manipulator.AddRotationDelta(new Vector2(100f, 0f));
            yield return _settle;
            Quaternion atRelease = _body.rotation;

            _manipulator.EndHold();
            _manipulator.AddRotationDelta(new Vector2(500f, 500f)); // Must be dropped.
            _manipulator.AddRollDelta(500f);
            yield return _settle;

            Assert.AreEqual(0f, Quaternion.Angle(atRelease, _body.rotation), 0.05f,
                "Post-release deltas must not rotate the stone.");
            Assert.IsNull(_manipulator.HeldBody, "Release must clear the held body.");
        }

        // ============================ helpers ============================

        private Quaternion _spinBaseline = Quaternion.identity;

        private Quaternion RelativeSpin()
        {
            Quaternion now = _body.rotation;
            Quaternion spin = now * Quaternion.Inverse(_spinBaseline);
            _spinBaseline = now;
            return spin;
        }

        private static void AssertAxisParallel(Quaternion delta, Vector3 expectedAxis, string message)
        {
            delta.ToAngleAxis(out float angle, out Vector3 axis);
            Assert.Greater(angle, 0.5f, "Precondition: a real rotation must have occurred. " + message);
            Assert.Greater(Mathf.Abs(Vector3.Dot(axis.normalized, expectedAxis.normalized)),
                0.999f, message);
        }

        private void BuildRig()
        {
            _root = new GameObject("Rotator3DTestRig");

            var camGo = new GameObject("TestCamera");
            camGo.transform.SetParent(_root.transform, false);
            camGo.transform.position = new Vector3(BaseX, 1f, BaseZ - 4f);
            camGo.transform.rotation = Quaternion.identity;
            _camera = camGo.AddComponent<Camera>();
            _camera.enabled = false; // Axes only; never renders.

            var stoneGo = new GameObject("HeldStone");
            stoneGo.transform.SetParent(_root.transform, false);
            stoneGo.transform.position = new Vector3(BaseX, 1f, BaseZ);
            stoneGo.AddComponent<BoxCollider>();
            _body = stoneGo.AddComponent<Rigidbody>();
            // COM must be configured while dynamic: Unity silently ignores
            // centerOfMass sets on an already-kinematic body (probed 1.3).
            _body.automaticCenterOfMass = false;
            _body.centerOfMass = ConfiguredCom;
            _body.isKinematic = true; // Frozen held state; MoveRotation only reorients.
            _spinBaseline = _body.rotation;

            var manipGo = new GameObject("RotationManipulator");
            manipGo.transform.SetParent(_root.transform, false);
            _manipulator = manipGo.AddComponent<Object3DRotationManipulator>();
            _manipulator.SetCamera(_camera);
            _manipulator.BeginHold(stoneGo);
        }
    }
}
