using UnityEngine;
using UnityEngine.Animations;

namespace AnimationSystem.Jobs
{
    public struct LocomotionAnimationJob : IAnimationJob
    {
        public LocomotionAnimationJobConfig Config;
            
        public float RightFootStepWeight;
        public float LeftFootStepWeight;
        
        public Quaternion RightFootRotation;
        public Vector3 RightFootStartPosition;
        public Vector3 RightFootEndPosition;
        
        public Quaternion LeftFootRotation;
        public Vector3 LeftFootStartPosition;
        public Vector3 LeftFootEndPosition;
        
        public TransformStreamHandle RightFootTarget;
        public TransformStreamHandle LeftFootTarget;
        
        public void ProcessAnimation(AnimationStream stream)
        {
            var rightFootSinWeight = Mathf.Sin(RightFootStepWeight * Mathf.PI);
            var rightFootAdditionHeight = new Vector3(0, rightFootSinWeight * Config.StepHeight, 0);
            var rightFootPosition = Vector3.Lerp(RightFootStartPosition, RightFootEndPosition, RightFootStepWeight);
            var rightFootFinalPosition = rightFootPosition +  rightFootAdditionHeight;
            
            RightFootTarget.SetGlobalTR(
                stream,
                rightFootFinalPosition,
                RightFootRotation,
                // Vector3.one,
                false);
            
            var leftFootSinWeight = Mathf.Sin(LeftFootStepWeight * Mathf.PI);
            var leftFootAdditionHeight = new Vector3(0, leftFootSinWeight * Config.StepHeight, 0);
            var leftFootPosition = Vector3.Lerp(LeftFootStartPosition, LeftFootEndPosition, LeftFootStepWeight);
            var leftFootFinalPosition = leftFootPosition +  leftFootAdditionHeight;
            
            LeftFootTarget.SetGlobalTR(
                stream,
                leftFootFinalPosition,
                LeftFootRotation,
                // Vector3.one,
                false);
        }

        public void ProcessRootMotion(AnimationStream stream)
        {
            // ignored
        }
    }
}