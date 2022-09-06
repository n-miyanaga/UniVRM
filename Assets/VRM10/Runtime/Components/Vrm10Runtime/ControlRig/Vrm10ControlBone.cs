using System;
using System.Collections.Generic;
using UnityEngine;

namespace UniVRM10
{
    /// <summary>
    /// The control bone of the control rig.
    ///
    /// このクラスのヒエラルキーが 正規化された TPose を表している。
    /// 同時に、元のヒエラルキーの初期回転を保持する。
    /// Apply 関数で、再帰的に正規化済みのローカル回転から初期回転を加味したローカル回転を作って適用する。
    /// </summary>
    public sealed class Vrm10ControlBone
    {
        /// <summary>
        /// このボーンに紐づく種類。
        /// </summary>
        public HumanBodyBones BoneType { get; }

        /// <summary>
        /// コントロール対象のボーン Transform。
        /// </summary>
        public Transform ControlTarget { get; }

        /// <summary>
        /// コントロールのためのボーン Transform。
        ///
        /// VRM の T-Pose 姿勢をしているときに、回転とスケールが初期値になっている（正規化）。
        /// このボーンに対して localRotation を代入し、コントロールを行う。
        /// </summary>
        public Transform ControlBone { get; }

        private readonly Quaternion _initialTargetLocalRotation;
        private readonly Quaternion _initialTargetGlobalRotation;
        private readonly List<Vrm10ControlBone> _children = new List<Vrm10ControlBone>();

        internal Vrm10ControlBone(Transform controlTarget, HumanBodyBones boneType)
        {
            if (boneType == HumanBodyBones.LastBone)
            {
                throw new ArgumentNullException();
            }
            if (controlTarget == null)
            {
                throw new ArgumentNullException();
            }

            BoneType = boneType;
            ControlTarget = controlTarget;
            ControlBone = new GameObject(boneType.ToString()).transform;
            ControlBone.position = controlTarget.position;
            _initialTargetLocalRotation = controlTarget.localRotation;
            _initialTargetGlobalRotation = controlTarget.rotation;
        }

        public static Vrm10ControlBone Build(UniHumanoid.Humanoid humanoid, Dictionary<HumanBodyBones, Vrm10ControlBone> boneMap)
        {
            var hips = new Vrm10ControlBone(humanoid.Hips, HumanBodyBones.Hips);
            boneMap.Add(HumanBodyBones.Hips, hips);

            foreach (Transform child in humanoid.Hips)
            {
                BuildRecursively(humanoid, child, hips, boneMap);
            }

            return hips;
        }

        private static void BuildRecursively(UniHumanoid.Humanoid humanoid, Transform current, Vrm10ControlBone parent, Dictionary<HumanBodyBones, Vrm10ControlBone> boneMap)
        {
            if (humanoid.TryGetBoneForTransform(current, out var bone))
            {

                // ヒューマンボーンだけを対象にするので、
                // parent が current の直接の親でない場合がある。
                // ワールド回転 parent^-1 * current からローカル回転を算出する。
                var parentInverse = Quaternion.Inverse(parent.ControlTarget.rotation);

                var newBone = new Vrm10ControlBone(current, bone);
                newBone.ControlBone.SetParent(parent.ControlBone, true);
                parent._children.Add(newBone);
                parent = newBone;
                boneMap.Add(bone, newBone);
            }

            foreach (Transform child in current)
            {
                BuildRecursively(humanoid, child, parent, boneMap);
            }
        }

        /// <summary>
        /// 親から再帰的にNormalized の ローカル回転を初期回転を加味して Target に適用する。
        /// </summary>
        internal void ProcessRecursively()
        {
            ControlTarget.localRotation = _initialTargetLocalRotation * Quaternion.Inverse(_initialTargetGlobalRotation) * ControlBone.localRotation * _initialTargetGlobalRotation;
            foreach (var child in _children)
            {
                child.ProcessRecursively();
            }
        }
    }
}
