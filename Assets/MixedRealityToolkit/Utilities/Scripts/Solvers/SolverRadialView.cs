﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using MixedRealityToolkit.InputModule.Utilities;
using UnityEngine;

namespace MixedRealityToolkit.Utilities.Solvers
{
    /// <summary>
    /// RadialViewPoser solver locks a tag-along type object within a view cone
    /// </summary>
    public class SolverRadialView : Solver
    {
        #region public enums
        public enum ReferenceDirectionEnum
        {
            /// <summary>
            /// Orient towards head including roll, pitch and yaw
            /// </summary>
            ObjectOriented,
            /// <summary>
            /// Orient toward head but ignore roll
            /// </summary>
            FacingWorldUp,
            /// <summary>
            /// Orient torwards the head movement direction found in CameraMotionInfo singleton
            /// </summary>
            HeadMoveDirection,
            /// <summary>
            /// Orient towards head but remain vertical or gravity aligned
            /// </summary>
            GravityAligned
        }
        #endregion

        #region public members
        [Tooltip("Which direction to position the element relative to: HeadOriented rolls with the head, HeadFacingWorldUp view dir but ignores head roll, and HeadMoveDirection uses the direction the head last moved without roll")]
        public ReferenceDirectionEnum ReferenceDirection = ReferenceDirectionEnum.FacingWorldUp;

        [Tooltip("Min distance from eye to position element around, i.e. the sphere radius")]
        public float MinDistance = 1f;
        [Tooltip("Max distance from eye to element")]
        public float MaxDistance = 2f;

        [Tooltip("The element will stay at least this far away from the center of view")]
        public float MinViewDegrees;
        [Tooltip("The element will stay at least this close to the center of view")]
        public float MaxViewDegrees = 30f;
        [Tooltip("Apply a different clamp to vertical FOV than horizontal.  Vertical = Horizontal * AspectV")]
        public float AspectV = 1f;

        [Tooltip("Option to ignore angle clamping")]
        public bool IgnoreAngleClamp;
        [Tooltip("Option to ignore distance clamping")]
        public bool IgnoreDistanceClamp;

        [Tooltip("If true, element will orient to ReferenceDirection, otherwise it will orient to ref pos (the head is the only option currently)")]
        public bool OrientToRefDir;
        #endregion

        /// <summary>
        ///   Reference Direction is the direction of the cone.  Position to the view direction, or the movement direction
        /// </summary>
        /// <returns>Vector3, the forward direction to use for positioning</returns>
        private Vector3 GetReferenceDirection()
        {
            Vector3 ret;
            if (ReferenceDirection == ReferenceDirectionEnum.HeadMoveDirection && solverHandler.TrackedObjectToReference == SolverHandler.TrackedObjectToReferenceEnum.Head)
            {
                ret = Camera.main.GetComponent<CameraMotionInfo>().MoveDirection;
            }
            else
            {
                ret = solverHandler.TransformTarget != null ? solverHandler.TransformTarget.forward : Vector3.forward;
            }

            return ret;
        }

        /// <summary>
        ///   Cone may roll with head, or not.
        /// </summary>
        /// <returns>Vector3, the up direction to use for orientation</returns>
        private Vector3 GetReferenceUp()
        {
            Vector3 ret = Vector3.up;
            if (ReferenceDirection == ReferenceDirectionEnum.ObjectOriented)
            {
                ret = solverHandler.TransformTarget != null ? solverHandler.TransformTarget.up : Vector3.up;
            }

            return ret;
        }

        private Vector3 GetReferencePoint()
        {
            return solverHandler.TransformTarget != null ? solverHandler.TransformTarget.position : Vector3.zero;
        }

        /// <summary>
        /// Solver update function used to orient to the user
        /// </summary>
        public override void SolverUpdate()
        {
            Vector3 desiredPos = WorkingPos;

            if (IgnoreAngleClamp)
            {
                if (IgnoreDistanceClamp)
                {
                    desiredPos = transform.position;
                }
                else
                {
                    GetDesiredOrientation_DistanceOnly(ref desiredPos);
                }
            }
            else
            {
                GetDesiredOrientation(ref desiredPos);
            }

            // Element orientation
            Vector3 refDirUp = GetReferenceUp();
            Quaternion desiredRot;

            if (OrientToRefDir)
            {
                desiredRot = Quaternion.LookRotation(GetReferenceDirection(), refDirUp);
            }
            else
            {
                Vector3 refPoint = GetReferencePoint();
                desiredRot = Quaternion.LookRotation(desiredPos - refPoint, refDirUp);
            }

            // If gravity aligned then zero out the x and z euler angles on the rotation
            if (ReferenceDirection == ReferenceDirectionEnum.GravityAligned)
            {
                desiredRot.x = desiredRot.z = 0f;
            }

            GoalPosition = desiredPos;
            GoalRotation = desiredRot;

            UpdateWorkingPosToGoal();
            UpdateWorkingRotToGoal();
        }

        /// <summary>
        ///   Optimized version of GetDesiredOrientation.  There should be a different solver for distance constraint though
        /// </summary>
        /// <param name="desiredPos"></param>
        private void GetDesiredOrientation_DistanceOnly(ref Vector3 desiredPos)
        {
            // Determine reference locations and directions
            Vector3 refPoint = GetReferencePoint();
            Vector3 elementPoint = transform.position;
            Vector3 elementDelta = elementPoint - refPoint;
            float elementDist = elementDelta.magnitude;
            Vector3 elementDir = elementDist > 0 ? elementDelta / elementDist : Vector3.one;

            // Clamp distance too
            float clampedDistance = Mathf.Clamp(elementDist, MinDistance, MaxDistance);

            if (!clampedDistance.Equals(elementDist))
            {
                desiredPos = refPoint + clampedDistance * elementDir;
            }
        }

        private void GetDesiredOrientation(ref Vector3 desiredPos)
        {
            // Determine reference locations and directions
            Vector3 refDir = GetReferenceDirection();
            Vector3 refDirUp = GetReferenceUp();
            Vector3 refPoint = GetReferencePoint();
            Vector3 elementPoint = transform.position;
            Vector3 elementDelta = elementPoint - refPoint;
            float elementDist = elementDelta.magnitude;
            Vector3 elementDir = elementDist > 0 ? elementDelta / elementDist : Vector3.one;
            float flip = Vector3.Dot(elementDelta, refDir);

            // Generate basis: First get axis perp to refDir pointing toward element
            Vector3 elementDirPerp = (elementDir - refDir);
            elementDirPerp -= refDir * Vector3.Dot(elementDirPerp, refDir);
            elementDirPerp.Normalize();

            // Calculate the clamping angles, accounting for aspect (need the angle relative to view plane)
            float angle = Vector3.Angle(elementDirPerp, refDirUp);
            float aspectScale = Mathf.Lerp(AspectV, 1f, Mathf.Abs(Mathf.Sin(angle * Mathf.Deg2Rad)));

            // Calculate the current angle
            float angDegree = Vector3.Angle(elementDir, refDir);
            float angDegreeClamped = Mathf.Clamp(angDegree, MinViewDegrees * aspectScale, MaxViewDegrees * aspectScale);

            // Clamp distance too, if desired
            float clampedDistance = IgnoreDistanceClamp ? elementDist : Mathf.Clamp(elementDist, MinDistance, MaxDistance);

            // If the angle was clamped, do some special update stuff
            if (flip < 0)
            {
                desiredPos = refPoint + refDir;
            }
            else if (!angDegree.Equals(angDegreeClamped))
            {
                float angRad = angDegreeClamped * Mathf.Deg2Rad;

                // Calculate new position
                desiredPos = refPoint + clampedDistance * (refDir * Mathf.Cos(angRad) + elementDirPerp * Mathf.Sin(angRad));
            }
            else if (!clampedDistance.Equals(elementDist))
            {
                // Only need to apply distance
                desiredPos = refPoint + clampedDistance * elementDir;
            }
        }
    }
}