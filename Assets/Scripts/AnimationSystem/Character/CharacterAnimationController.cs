using AnimationBalanceSystem;
using AnimationPairsSearchSystem;
using AnimationSystem.Jobs;
using Infrastructure.Interfaces;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Animations.Rigging;
using UnityEngine.Playables;

namespace AnimationSystem.Character
{
    [ExecuteInEditMode]
    public class CharacterAnimationController : MonoBehaviour,
        IInitializable,
        IUpdatable,
        IDestroyable
    {
        private const int LOCOMOTION_IDLE_ANIMATION_INDEX = 0;
        private const int LOCOMOTION_STEP_FORWARD_RIGHT_ANIMATION_INDEX = 1;
        private const int LOCOMOTION_STEP_FORWARD_LEFT_ANIMATION_INDEX = 2;
        private const int LOCOMOTION_STEP_BACKWARD_RIGHT_ANIMATION_INDEX = 3;
        private const int LOCOMOTION_STEP_BACKWARD_LEFT_ANIMATION_INDEX = 4;
        
        private const int POLEARM_IDLE_ANIMATION_INDEX = 0;
        private const int POLEARM_SWING_ANIMATION_INDEX = 1;
        private const int POLEARM_STRIKE_ANIMATION_INDEX = 2;
        
        private const int LOCOMOTION_LAYER_INDEX = 0;
        private const int POLEARM_LAYER_INDEX = 1;
        
        [Header("References")]
        [SerializeField] private Animator animator;
        [SerializeField] private RigBuilder rigBuilder;
        [SerializeField] private RootMotionHandlerView motionHandler;
        [SerializeField] private CenterOfMassView centerOfMass;
        
        [Header("Strike Clips")]
        [SerializeField] private AnimationClip polearmIdleClip;
        [SerializeField] private AnimationSwingStrikePair polearmSwingStrikePair;
        [SerializeField] private AvatarMask polearmAvatarMask;
        
        [Header("Locomotion Clips")]
        [SerializeField] private AnimationClip locomotionIdleClip;
        
        [SerializeField] private AnimationClip locomotionStepForwardRightClip;
        [SerializeField] private AnimationClip locomotionStepForwardLeftClip;
        [SerializeField] private AnimationClip locomotionStepBackwardRightClip;
        [SerializeField] private AnimationClip locomotionStepBackwardLeftClip;
        
        [SerializeField] private AvatarMask locomotionAvatarMask;

        [Header("Settings")]
        [Range(0f, 1f)]
        [SerializeField] private float idleToSwingBlendTime;
        [Range(0f, 1f)]
        [SerializeField] private float strikeToIdleBlendTime;
        [Range(0f, 1f)]
        [SerializeField] private float swingToIdleBlendTime;
        [Range(0f, 1f)]
        [SerializeField] private float weaponMovingTime;
        [Range(0f, 1f)]
        [SerializeField] private float handsMovingTime;
        
        [Header("Polearm Settings")]
        [SerializeField] private float strikeEndTime;
        [SerializeField] private float startWeaponOffset;
        
        [SerializeField] private Transform hitTarget;
        [SerializeField] private Transform weaponBone;
        [SerializeField] private Transform weapon;
        [SerializeField] private Transform weaponObject;
        [SerializeField] private MeshFilter weaponMeshFilter;
        
        [Header("Hands Settings")]
        [SerializeField] private Transform leftHandRotationTransform;
        [SerializeField] private Transform rightHandRotationTransform;
        [SerializeField] private Transform leftHandPositionTransform;
        [SerializeField] private Transform rightHandPositionTransform;

        [SerializeField] private Transform leftHandTarget;
        [SerializeField] private Transform rightHandTarget;
        
        [Header("Locomotion Settings")]
        [SerializeField] private float stepHeight;
        [SerializeField] private float stepStartTimeOffset;
        
        [SerializeField] private float minStepLength = 0.3f;
        [SerializeField] private float maxStepLength = 0.7f;
        
        [SerializeField] private Transform rightFootBone;
        [SerializeField] private Transform rightFootTarget;
        
        [SerializeField] private Transform leftFootBone;
        [SerializeField] private Transform leftFootTarget;

        [SerializeField] private ChainIKConstraint rightFootIK;
        
        [Header("Sampler")]
        [SerializeField] private Transform samplerWeaponBone;
        [SerializeField] private Transform samplerStrikePivotBone;
        [SerializeField] private Transform samplerShouldersBone;
        [SerializeField] private Transform samplerRightFootBone;
        [SerializeField] private Transform samplerLeftFootBone;
        
        [SerializeField] private Animator samplerAnimator;
        [SerializeField] private RootMotionHandlerView samplerMotionHandler;

        private float StrikeAnimationTime => idleToSwingBlendTime +
                                             polearmSwingStrikePair.SwingLength +
                                             polearmSwingStrikePair.BlendTime +
                                             polearmSwingStrikePair.StrikeLength +
                                             (_useHands ? handsMovingTime + weaponMovingTime : 0);
        
        private PlayableGraph _graph;
        private AnimationLayerMixerPlayable _mixer;
        private AnimationMixerPlayable _polearmMixer;
        private AnimationMixerPlayable _locomotionMixer;
        private AnimationClipPlayable _locomotionIdleClipPlayable;
        private AnimationClipPlayable _locomotionStepForwardRightClipPlayable;
        private AnimationClipPlayable _locomotionStepForwardLeftClipPlayable;
        private AnimationClipPlayable _locomotionStepBackwardRightClipPlayable;
        private AnimationClipPlayable _locomotionStepBackwardLeftClipPlayable;
        private AnimationClipPlayable _polearmIdleClipPlayable;
        private AnimationClipPlayable _polearmSwingClipPlayable;
        private AnimationClipPlayable _polearmStrikeClipPlayable;
        private AnimationScriptPlayable _polearmAnimationPlayable;
        private AnimationScriptPlayable _locomotionAnimationPlayable;
        private PolearmAnimationJob _polearmAnimationJob;
        private LocomotionAnimationJob _locomotionAnimationJob;
        
        private PlayableGraph _samplerGraph;
        private AnimationLayerMixerPlayable _sampleMixer;
        private AnimationClipPlayable _sampleLocomotionStepClipPlayable;
        private AnimationClipPlayable _sampleLocomotionIdleClipPlayable;
        private AnimationClipPlayable _samplePolearmIdleClipPlayable;
        private AnimationClipPlayable _samplePolearmStrikeClipPlayable;

        private CharacterPolearmAnimationState _polearmAnimationState;
        private CharacterLocomotionAnimationState _locomotionAnimationState;
        
        private float _locomotionBlendPassedTime;
        
        private float _polearmPassedTime;
        private float _polearmBlendPassedTime;
        private float _polearmStrikePassedTime;
        private float _polearmMoveHandsPassedTime;

        private bool _isStepPlaying;
        private float _stepLength;

        private bool _useHands;
        private bool _useStep;
        private bool _isCurrentFootRight;
        private int _steps;
        
        private Vector3 _defaultSamplerPosition;

        [Button]
        private void PlayStrike()
        {
            _isStepPlaying = false;
            _polearmPassedTime = 0;
            _polearmBlendPassedTime = 0;
            _polearmMoveHandsPassedTime = 0;
            _polearmSwingClipPlayable.SetSpeed(0);
            _polearmSwingClipPlayable.SetTime(0);
            SetPolearmJobMovingHandsWeight(0, 0);
            
            SampleStrikeEnd(false, out var bakedWeaponEndPos, out var bakedWeaponEndRot);
            
            var strikePivotPos = samplerStrikePivotBone.position;
            var weaponLocalOffset = new Vector3(0, (bakedWeaponEndPos - samplerStrikePivotBone.position).magnitude, 0);
            var targetPos = hitTarget.position;
            var targetDir = (targetPos - strikePivotPos).normalized;
            var deltaRot = Quaternion.FromToRotation(bakedWeaponEndRot * Vector3.up, targetDir);
            var targetWeaponRot = deltaRot * bakedWeaponEndRot;
            var targetWeaponPos = strikePivotPos + targetWeaponRot * weaponLocalOffset;
            var deltaPos = targetWeaponPos - bakedWeaponEndPos;
            var currentDist = weaponObject.localPosition.y;
            var targetDist = (targetWeaponPos - targetPos).magnitude - 1;
            var deltaDist = targetDist - currentDist;
            _useStep = deltaDist >= minStepLength;
            _stepLength = Mathf.Clamp(deltaDist, minStepLength, maxStepLength);

            if (_useStep)
            {
                SampleStrikeEnd(true, out bakedWeaponEndPos, out bakedWeaponEndRot);
                strikePivotPos = samplerStrikePivotBone.position;
                weaponLocalOffset = new Vector3(0, (bakedWeaponEndPos - samplerStrikePivotBone.position).magnitude, 0);
                targetDir = (targetPos - strikePivotPos).normalized;
                deltaRot = Quaternion.FromToRotation(bakedWeaponEndRot * Vector3.up, targetDir);
                targetWeaponRot = deltaRot * bakedWeaponEndRot;
                targetWeaponPos = strikePivotPos + targetWeaponRot * weaponLocalOffset;
                deltaPos = targetWeaponPos - bakedWeaponEndPos;
                currentDist = weaponObject.localPosition.y;
                targetDist = (targetWeaponPos - targetPos).magnitude - 1;
            }
            
            _useHands = !Mathf.Approximately(currentDist, targetDist);
            
            var polearmJob = _polearmAnimationPlayable.GetJobData<PolearmAnimationJob>();
            polearmJob.WeaponCorrectionDeltaRotation = deltaRot;
            polearmJob.WeaponCorrectionDeltaPosition = deltaPos;
            polearmJob.WeaponObjectLocalStartPosition = new Vector3(0, currentDist, 0);
            polearmJob.WeaponObjectLocalEndPosition = new Vector3(0, targetDist, 0);
            _polearmAnimationPlayable.SetJobData(polearmJob);
            
            ChangePolearmState(_useHands ?
                CharacterPolearmAnimationState.MoveHands :
                CharacterPolearmAnimationState.IdleToSwing);
        }
        

        private void PlayStepForward()
        {
            motionHandler.ChangeAnimation();
            
            _locomotionStepForwardRightClipPlayable.SetSpeed(0);
            _locomotionStepForwardLeftClipPlayable.SetSpeed(0);
            _locomotionStepBackwardRightClipPlayable.SetSpeed(0);
            _locomotionStepBackwardLeftClipPlayable.SetSpeed(0);

            _steps++;
            _isStepPlaying = true;
            _locomotionStepForwardRightClipPlayable.SetTime(0);
            _locomotionStepForwardRightClipPlayable.SetSpeed(1);
            SetLocomotionJobStepWeight(0);

            var startPosition = rightFootBone.position;
            var endPosition = rightFootBone.position + new Vector3(0, 0, _stepLength);
                
            var locomotionJob = _locomotionAnimationPlayable.GetJobData<LocomotionAnimationJob>();
            locomotionJob.RightFootStartPosition = startPosition;
            locomotionJob.RightFootEndPosition = endPosition;
            _locomotionAnimationPlayable.SetJobData(locomotionJob);
            
            ChangeLocomotionState(CharacterLocomotionAnimationState.StepForwardRight);
        }
        [Button]
        private void PlaySwingToIdleWithStep()
        {
            motionHandler.ChangeAnimation();
            
            _locomotionStepForwardRightClipPlayable.SetSpeed(0);
            _locomotionStepForwardLeftClipPlayable.SetSpeed(0);
            _locomotionStepBackwardRightClipPlayable.SetSpeed(0);
            _locomotionStepBackwardLeftClipPlayable.SetSpeed(0);
            
            _steps--;
            _isStepPlaying = true;
            _polearmBlendPassedTime = 0;
            _locomotionStepBackwardLeftClipPlayable.SetTime(0);
            _locomotionStepBackwardLeftClipPlayable.SetSpeed(1);
            _polearmSwingClipPlayable.SetSpeed(0);
            SetLocomotionJobStepWeight(0);
            
            var startPosition = leftFootBone.position;
            var endPosition = leftFootBone.position + new Vector3(0, 0, -maxStepLength);
                
            var locomotionJob = _locomotionAnimationPlayable.GetJobData<LocomotionAnimationJob>();
            locomotionJob.LeftFootStartPosition = startPosition;
            locomotionJob.LeftFootEndPosition = endPosition;
            _locomotionAnimationPlayable.SetJobData(locomotionJob);
            
            ChangePolearmState(CharacterPolearmAnimationState.SwingToIdle);
            ChangeLocomotionState(CharacterLocomotionAnimationState.StepBackwardLeft);
        }
        private void PlayStepToIdle()
        {
            motionHandler.ChangeAnimation();
            
            _locomotionStepForwardRightClipPlayable.SetSpeed(0);
            _locomotionStepForwardLeftClipPlayable.SetSpeed(0);
            _locomotionStepBackwardRightClipPlayable.SetSpeed(0);
            _locomotionStepBackwardLeftClipPlayable.SetSpeed(0);
            
            var stepInfo = GetStepToIdleInfo();
            stepInfo.clipPlayable.SetTime(0);
            stepInfo.clipPlayable.SetSpeed(1);
            SetLocomotionJobStepWeight(1);
            
            ChangeLocomotionState(CharacterLocomotionAnimationState.StepToIdle);
        }
        
        public void Initialize()
        {
            ChangePolearmState(CharacterPolearmAnimationState.Idle);
            CreateSamplerGraph();
            CreatePolearmJob();
            CreateLocomotionJob();
            CreateGraph();
        }
        public void ManualUpdate()
        {
            UpdateStrikeAnimation();

            // if (_polearmAnimationState is
            //         CharacterPolearmAnimationState.IdleToSwing or
            //         CharacterPolearmAnimationState.Swing &&
            //     !centerOfMass.IsCenterOfMassInsideSupportArea())
            // {
            //     PlaySwingToIdleWithStep();
            // }
            //
            // if (_locomotionAnimationState == CharacterLocomotionAnimationState.StepBackwardLeft &&
            //     _locomotionBlendPassedTime >= _locomotionStepBackwardLeftClipPlayable.GetAnimationClip().length)
            // {
            //     PlayStepToIdle();
            // }

            if (_useStep &&
                !_isStepPlaying &&
                // _polearmAnimationState is
                //     CharacterPolearmAnimationState.SwingToStrike or
                //     CharacterPolearmAnimationState.Strike &&
                StrikeAnimationTime - _polearmPassedTime - stepStartTimeOffset <= _locomotionStepForwardRightClipPlayable.GetAnimationClip().length)
            {
                PlayStepForward();
            }

            if (Mathf.Abs(_steps) % 2 == 1 &&
                _polearmAnimationState == CharacterPolearmAnimationState.StrikeToIdle &&
                _polearmStrikeClipPlayable.GetTime() >= polearmSwingStrikePair.StrikeLength &&
                _locomotionBlendPassedTime >= _locomotionStepForwardRightClipPlayable.GetAnimationClip().length)
            {
                PlayStepToIdle();
            }

            UpdateLocomotionAnimation();
        }

        private void UpdateStrikeAnimation()
        {
            switch (_polearmAnimationState)
            {
                case CharacterPolearmAnimationState.Idle:
                {
                    return;
                }
                case CharacterPolearmAnimationState.MoveHands:
                {
                    var maxTime = weaponMovingTime + handsMovingTime;
                    var handsPassedTime = _polearmMoveHandsPassedTime - weaponMovingTime;
                    var deltaWeaponWeight = Mathf.Clamp01(_polearmMoveHandsPassedTime / weaponMovingTime);
                    var deltaHandsWeight = Mathf.Clamp01(_polearmMoveHandsPassedTime < weaponMovingTime ?
                        1 :
                        (handsMovingTime - handsPassedTime) / handsMovingTime);

                    SetPolearmJobMovingHandsWeight(deltaWeaponWeight, deltaHandsWeight);

                    _polearmMoveHandsPassedTime += Time.deltaTime;
                    
                    if (_polearmMoveHandsPassedTime >= maxTime)
                    {
                        _polearmBlendPassedTime = 0;
                        _polearmSwingClipPlayable.SetTime(0);
                        _polearmSwingClipPlayable.SetSpeed(0);
                        
                        SetPolearmJobMovingHandsWeight(1, 0);
                        
                        ChangePolearmState(CharacterPolearmAnimationState.IdleToSwing);
                    }
                    
                    break;
                }
                case CharacterPolearmAnimationState.IdleToSwing:
                {
                    var deltaWeight = Mathf.Clamp01(_polearmBlendPassedTime / idleToSwingBlendTime);

                    SetPolearmAnimationWeights(1f - deltaWeight, deltaWeight, 0);
                    
                    _polearmBlendPassedTime += Time.deltaTime;

                    if (deltaWeight >= 1f)
                    {
                        _polearmBlendPassedTime = 0;
                        _polearmSwingClipPlayable.SetSpeed(1);
                        
                        ChangePolearmState(CharacterPolearmAnimationState.Swing);
                    }
                    
                    break;
                }
                case CharacterPolearmAnimationState.Swing:
                {
                    _polearmBlendPassedTime += Time.deltaTime;
                    
                    if (_polearmSwingClipPlayable.GetTime() >= polearmSwingStrikePair.SwingLength)
                    {
                        _polearmBlendPassedTime = 0;
                        _polearmStrikePassedTime = 0;
                        _polearmStrikeClipPlayable.SetTime(polearmSwingStrikePair.StrikeStartTime);
                        _polearmStrikeClipPlayable.SetSpeed(0);
                        
                        ChangePolearmState(CharacterPolearmAnimationState.SwingToStrike);
                    }
                    
                    break;
                }
                case CharacterPolearmAnimationState.SwingToStrike:
                {
                    var deltaWeight = Mathf.Clamp01(_polearmBlendPassedTime / polearmSwingStrikePair.BlendTime);

                    SetPolearmAnimationWeights(0, 1f - deltaWeight, deltaWeight);
                        
                    _polearmBlendPassedTime += Time.deltaTime;
                    
                    if (deltaWeight >= 1f)
                    {
                        _polearmBlendPassedTime = 0;
                        _polearmStrikeClipPlayable.SetSpeed(1);
                        
                        SetPolearmJobHitTargetWeight(0);

                        ChangePolearmState(CharacterPolearmAnimationState.Strike);
                    }
                    
                    break;
                }
                case CharacterPolearmAnimationState.Strike:
                {
                    SetPolearmJobHitTargetWeight(_polearmStrikePassedTime / polearmSwingStrikePair.StrikeLength);
                    
                    _polearmStrikePassedTime += Time.deltaTime;
                    _polearmBlendPassedTime += Time.deltaTime;
                    
                    if (_polearmStrikeClipPlayable.GetTime() >= polearmSwingStrikePair.StrikeLength)
                    {
                        _polearmBlendPassedTime = 0;
                        _polearmStrikePassedTime = strikeToIdleBlendTime;
                        _polearmIdleClipPlayable.SetTime(0);
                        _polearmIdleClipPlayable.SetSpeed(0);
                        
                        ChangePolearmState(CharacterPolearmAnimationState.StrikeEnd);
                    }
                    
                    break;
                }
                case CharacterPolearmAnimationState.StrikeEnd:
                {
                    _polearmBlendPassedTime += Time.deltaTime;
                    
                    if (_polearmBlendPassedTime >= strikeEndTime)
                    {
                        _polearmBlendPassedTime = 0;
                        
                        ChangePolearmState(CharacterPolearmAnimationState.StrikeToIdle);
                    }
                    
                    break;
                }
                case CharacterPolearmAnimationState.StrikeToIdle:
                {
                    var deltaWeight = Mathf.Clamp01(_polearmBlendPassedTime / strikeToIdleBlendTime);

                    SetPolearmAnimationWeights(deltaWeight, 0, 1f - deltaWeight);
                    SetPolearmJobHitTargetWeight(_polearmStrikePassedTime / strikeToIdleBlendTime);
                    
                    _polearmStrikePassedTime -= Time.deltaTime;
                    _polearmBlendPassedTime += Time.deltaTime;
                    
                    if (deltaWeight >= 1f)
                    {
                        _polearmBlendPassedTime = 0;
                        _polearmIdleClipPlayable.SetSpeed(1);
                        
                        SetPolearmJobHitTargetWeight(0);
                        
                        ChangePolearmState(CharacterPolearmAnimationState.Idle);
                    }
                    
                    break;
                }
                case CharacterPolearmAnimationState.SwingToIdle:
                {
                    var deltaWeight = Mathf.Clamp01(_polearmBlendPassedTime / swingToIdleBlendTime);

                    SetPolearmAnimationWeights(deltaWeight, 1f - deltaWeight, 0);
                    
                    _polearmBlendPassedTime += Time.deltaTime;

                    if (deltaWeight >= 1)
                    {
                        _polearmBlendPassedTime = 0;
                        _polearmIdleClipPlayable.SetSpeed(1);
                        
                        ChangePolearmState(CharacterPolearmAnimationState.Idle);
                    }
                    
                    break;
                }
            }

            _polearmPassedTime += Time.deltaTime;
        }
        private void UpdateLocomotionAnimation()
        {
            switch (_locomotionAnimationState)
            {
                case CharacterLocomotionAnimationState.Idle:
                {
                    return;
                }
                case CharacterLocomotionAnimationState.StepForwardRight:
                {
                    var deltaWeight = Mathf.Clamp01(_locomotionBlendPassedTime / _locomotionStepForwardLeftClipPlayable.GetAnimationClip().length);
                    var sinWeight = Mathf.Sin(deltaWeight * Mathf.PI + Mathf.PI);

                    SetLocomotionAnimationWeights(0, 1, CharacterLocomotionAnimationState.StepForwardRight);
                    SetLocomotionJobStepWeight(deltaWeight);
                    SetFootIKWeight(sinWeight);
                    
                    break;
                }
                case CharacterLocomotionAnimationState.StepBackwardLeft:
                {
                    var deltaWeight = Mathf.Clamp01(_locomotionBlendPassedTime / _locomotionStepBackwardLeftClipPlayable.GetAnimationClip().length);
                    var sinWeight = Mathf.Sin(deltaWeight * Mathf.PI + Mathf.PI);

                    SetLocomotionAnimationWeights(0, 1, CharacterLocomotionAnimationState.StepBackwardLeft);
                    SetLocomotionJobStepWeight(deltaWeight);
                    SetFootIKWeight(sinWeight);
                    
                    break;
                }
                case CharacterLocomotionAnimationState.StepToIdle:
                {
                    var stepInfo = GetStepToIdleInfo();
                    var deltaWeight = 1 - Mathf.Clamp01(_locomotionBlendPassedTime / stepInfo.clipPlayable.GetAnimationClip().length);
                    var sinWeight = Mathf.Sin(deltaWeight * Mathf.PI + Mathf.PI);
                    
                    SetLocomotionAnimationWeights(0, 1, stepInfo.stepType);
                    SetLocomotionJobStepWeight(deltaWeight);
                    SetFootIKWeight(sinWeight);
                    
                    if (deltaWeight <= 0)
                    {
                        _steps = 0;
                        
                        SetLocomotionJobStepWeight(0);
                        
                        ChangeLocomotionState(CharacterLocomotionAnimationState.Idle);
                    }
                    
                    break;
                }
            }
            
            _locomotionBlendPassedTime += Time.deltaTime;
        }
        
        private void CreateSamplerGraph()
        {
            _samplerGraph = PlayableGraph.Create("Sampler Animation");
            _samplerGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            _sampleMixer = AnimationLayerMixerPlayable.Create(_samplerGraph, 4);
            var output = AnimationPlayableOutput.Create(_samplerGraph, "Sampler Animation Output", samplerAnimator);
            
            _sampleLocomotionIdleClipPlayable = AnimationClipPlayable.Create(_samplerGraph, locomotionIdleClip);
            _sampleLocomotionStepClipPlayable = AnimationClipPlayable.Create(_samplerGraph, locomotionStepForwardRightClip);
            _samplePolearmIdleClipPlayable = AnimationClipPlayable.Create(_samplerGraph, polearmIdleClip);
            _samplePolearmStrikeClipPlayable = AnimationClipPlayable.Create(_samplerGraph, polearmSwingStrikePair.StrikeAnimation);
            
            _samplePolearmStrikeClipPlayable.SetTime(0);
            _sampleLocomotionStepClipPlayable.SetTime(0);
            
            _sampleLocomotionIdleClipPlayable.SetApplyFootIK(false);
            _sampleLocomotionStepClipPlayable.SetApplyFootIK(false);
            _samplePolearmIdleClipPlayable.SetApplyFootIK(false);
            _samplePolearmStrikeClipPlayable.SetApplyFootIK(false);
            
            _samplerGraph.Connect(_sampleLocomotionIdleClipPlayable, 0, _sampleMixer, 0);
            _samplerGraph.Connect(_sampleLocomotionStepClipPlayable, 0, _sampleMixer, 1);
            _samplerGraph.Connect(_samplePolearmIdleClipPlayable, 0, _sampleMixer, 2);
            _samplerGraph.Connect(_samplePolearmStrikeClipPlayable, 0, _sampleMixer, 3);
            
            _sampleMixer.SetLayerMaskFromAvatarMask(0, locomotionAvatarMask);
            _sampleMixer.SetLayerMaskFromAvatarMask(1, locomotionAvatarMask);
            _sampleMixer.SetLayerMaskFromAvatarMask(2, polearmAvatarMask);
            _sampleMixer.SetLayerMaskFromAvatarMask(3, polearmAvatarMask);
            _sampleMixer.SetInputWeight(0, 1f);
            _sampleMixer.SetInputWeight(1, 0f);
            _sampleMixer.SetInputWeight(2, 1f);
            _sampleMixer.SetInputWeight(3, 0f);
            _sampleMixer.SetSpeed(0);
            
            output.SetSourcePlayable(_sampleMixer);

            _samplerGraph.Play();
            _samplerGraph.Evaluate(0);

            _defaultSamplerPosition = samplerAnimator.gameObject.transform.position;
        }
        private void CreateGraph()
        {
            _graph = PlayableGraph.Create("Animation");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            _mixer = AnimationLayerMixerPlayable.Create(_graph, 2);
            _polearmMixer = AnimationMixerPlayable.Create(_graph, 3);
            _locomotionMixer = AnimationMixerPlayable.Create(_graph, 5);
            var output = AnimationPlayableOutput.Create(_graph, "Animation Output", animator);
            
            _locomotionIdleClipPlayable = AnimationClipPlayable.Create(_graph, locomotionIdleClip);
            _locomotionStepForwardRightClipPlayable = AnimationClipPlayable.Create(_graph, locomotionStepForwardRightClip);
            _locomotionStepForwardLeftClipPlayable = AnimationClipPlayable.Create(_graph, locomotionStepForwardLeftClip);
            _locomotionStepBackwardRightClipPlayable = AnimationClipPlayable.Create(_graph, locomotionStepBackwardRightClip);
            _locomotionStepBackwardLeftClipPlayable = AnimationClipPlayable.Create(_graph, locomotionStepBackwardLeftClip);
            
            _polearmIdleClipPlayable = AnimationClipPlayable.Create(_graph, polearmIdleClip);
            _polearmSwingClipPlayable = AnimationClipPlayable.Create(_graph, polearmSwingStrikePair.SwingAnimation);
            _polearmStrikeClipPlayable = AnimationClipPlayable.Create(_graph, polearmSwingStrikePair.StrikeAnimation);
            
            _locomotionIdleClipPlayable.SetApplyFootIK(false);
            _locomotionStepForwardRightClipPlayable.SetApplyFootIK(false);
            _locomotionStepForwardLeftClipPlayable.SetApplyFootIK(false);
            _locomotionStepBackwardRightClipPlayable.SetApplyFootIK(false);
            _locomotionStepBackwardLeftClipPlayable.SetApplyFootIK(false);
            _polearmIdleClipPlayable.SetApplyFootIK(false);
            _polearmSwingClipPlayable.SetApplyFootIK(false);
            _polearmStrikeClipPlayable.SetApplyFootIK(false);

            _graph.Connect(_locomotionIdleClipPlayable, 0, _locomotionMixer, LOCOMOTION_IDLE_ANIMATION_INDEX);
            _graph.Connect(_locomotionStepForwardRightClipPlayable, 0, _locomotionMixer, LOCOMOTION_STEP_FORWARD_RIGHT_ANIMATION_INDEX);
            _graph.Connect(_locomotionStepForwardLeftClipPlayable, 0, _locomotionMixer, LOCOMOTION_STEP_FORWARD_LEFT_ANIMATION_INDEX);
            _graph.Connect(_locomotionStepBackwardRightClipPlayable, 0, _locomotionMixer, LOCOMOTION_STEP_BACKWARD_RIGHT_ANIMATION_INDEX);
            _graph.Connect(_locomotionStepBackwardLeftClipPlayable, 0, _locomotionMixer, LOCOMOTION_STEP_BACKWARD_LEFT_ANIMATION_INDEX);
            
            _graph.Connect(_polearmIdleClipPlayable, 0, _polearmMixer, POLEARM_IDLE_ANIMATION_INDEX);
            _graph.Connect(_polearmSwingClipPlayable, 0, _polearmMixer, POLEARM_SWING_ANIMATION_INDEX);
            _graph.Connect(_polearmStrikeClipPlayable, 0, _polearmMixer, POLEARM_STRIKE_ANIMATION_INDEX);
            
            _locomotionMixer.SetInputWeight(LOCOMOTION_IDLE_ANIMATION_INDEX, 1f);
            _locomotionMixer.SetInputWeight(LOCOMOTION_STEP_FORWARD_RIGHT_ANIMATION_INDEX, 0f);
            _locomotionMixer.SetInputWeight(LOCOMOTION_STEP_FORWARD_LEFT_ANIMATION_INDEX, 0f);
            _locomotionMixer.SetInputWeight(LOCOMOTION_STEP_BACKWARD_RIGHT_ANIMATION_INDEX, 0f);
            _locomotionMixer.SetInputWeight(LOCOMOTION_STEP_BACKWARD_LEFT_ANIMATION_INDEX, 0f);
            
            _polearmMixer.SetInputWeight(POLEARM_IDLE_ANIMATION_INDEX, 1f);
            _polearmMixer.SetInputWeight(POLEARM_SWING_ANIMATION_INDEX, 0f);
            _polearmMixer.SetInputWeight(POLEARM_STRIKE_ANIMATION_INDEX, 0f);

            _graph.Connect(_locomotionMixer, 0, _mixer, LOCOMOTION_LAYER_INDEX);
            _graph.Connect(_polearmMixer, 0, _mixer, POLEARM_LAYER_INDEX);

            _mixer.SetInputWeight(LOCOMOTION_LAYER_INDEX, 1f);
            _mixer.SetInputWeight(POLEARM_LAYER_INDEX, 1f);
            
            _mixer.SetLayerMaskFromAvatarMask(LOCOMOTION_LAYER_INDEX, locomotionAvatarMask);
            _mixer.SetLayerMaskFromAvatarMask(POLEARM_LAYER_INDEX, polearmAvatarMask);
            
            _polearmAnimationPlayable = AnimationScriptPlayable.Create(_graph, _polearmAnimationJob, 1);
            _locomotionAnimationPlayable = AnimationScriptPlayable.Create(_graph, _locomotionAnimationJob, 1);
            
            _graph.Connect(_mixer, 0, _locomotionAnimationPlayable, 0);
            _graph.Connect(_locomotionAnimationPlayable, 0, _polearmAnimationPlayable, 0);
            
            _polearmAnimationPlayable.SetInputWeight(0, 1f);
            _locomotionAnimationPlayable.SetInputWeight(0, 1f);
            
            var riggedPlayable = rigBuilder.BuildPreviewGraph(_graph, _polearmAnimationPlayable);
            
            output.SetSourcePlayable(riggedPlayable);

            _graph.Play();
        }
        private void CreatePolearmJob()
        {
            var startWeaponObjectPositionOffset = new Vector3(0, startWeaponOffset, 0);
            var localShouldersPoint = samplerWeaponBone.InverseTransformPoint(samplerShouldersBone.position);
            var handsLocalOffset = new Vector3(0, localShouldersPoint.y, 0);
            
            var config = new PolearmAnimationJobConfig
            {
                HandsLocalOffset = handsLocalOffset,
            };
            
            _polearmAnimationJob = new PolearmAnimationJob
            {
                Config = config,
                WeaponObjectLocalStartPosition = startWeaponObjectPositionOffset,
                
                WeaponBone = animator.BindSceneTransform(weaponBone),
                Weapon = animator.BindStreamTransform(weapon),
                WeaponObject = animator.BindStreamTransform(weaponObject),
                LeftHandRotationTransform = animator.BindSceneTransform(leftHandRotationTransform),
                RightHandRotationTransform = animator.BindSceneTransform(rightHandRotationTransform),
                LeftHandPositionTransform = animator.BindSceneTransform(leftHandPositionTransform),
                RightHandPositionTransform = animator.BindSceneTransform(rightHandPositionTransform),
                LeftHandTarget = animator.BindStreamTransform(leftHandTarget),
                RightHandTarget = animator.BindStreamTransform(rightHandTarget)
            };
        }
        private void CreateLocomotionJob()
        {
            var rightFootStartPosition = samplerRightFootBone.position;
            var leftFootStartPosition = samplerLeftFootBone.position;
            var rightFootRotation = samplerRightFootBone.rotation;
            var leftFootRotation = samplerLeftFootBone.rotation;
            
            var config = new LocomotionAnimationJobConfig
            {
                StepHeight = stepHeight
            };
            
            _locomotionAnimationJob = new LocomotionAnimationJob
            {
                Config = config,

                RightFootRotation = rightFootRotation,
                LeftFootRotation = leftFootRotation,
                RightFootStartPosition = rightFootStartPosition,
                LeftFootStartPosition = leftFootStartPosition,
                RightFootTarget = animator.BindStreamTransform(rightFootTarget),
                LeftFootTarget = animator.BindStreamTransform(leftFootTarget)
            };
        }

        private void ChangePolearmState(CharacterPolearmAnimationState newState)
        {
            _polearmAnimationState = newState;
        }
        private void ChangeLocomotionState(CharacterLocomotionAnimationState newState)
        {
            _locomotionBlendPassedTime = 0;
            _locomotionAnimationState = newState;
        }
        
        private void SetPolearmAnimationWeights(float idle, float swing, float strike)
        {
            _polearmMixer.SetInputWeight(POLEARM_IDLE_ANIMATION_INDEX, idle);
            _polearmMixer.SetInputWeight(POLEARM_SWING_ANIMATION_INDEX, swing);
            _polearmMixer.SetInputWeight(POLEARM_STRIKE_ANIMATION_INDEX, strike);
        }
        private void SetPolearmJobHitTargetWeight(float hitTargetWeight)
        {
            var job = _polearmAnimationPlayable.GetJobData<PolearmAnimationJob>();
            job.HitTargetWeight = Mathf.Clamp01(hitTargetWeight);
            _polearmAnimationPlayable.SetJobData(job);
        }
        private void SetPolearmJobMovingHandsWeight(float weaponWeight, float handWeight)
        {
            var job = _polearmAnimationPlayable.GetJobData<PolearmAnimationJob>();
            job.WeaponLocalPositionEndWeight = Mathf.Clamp01(weaponWeight);
            job.HandWeaponWeight = Mathf.Clamp01(handWeight);
            _polearmAnimationPlayable.SetJobData(job);
        }

        private void SetLocomotionAnimationWeights(float idle, float step, CharacterLocomotionAnimationState stepType)
        {
            _locomotionMixer.SetInputWeight(LOCOMOTION_IDLE_ANIMATION_INDEX, idle);
            _locomotionMixer.SetInputWeight(LOCOMOTION_STEP_FORWARD_RIGHT_ANIMATION_INDEX, stepType == CharacterLocomotionAnimationState.StepForwardRight ? step : 0);
            _locomotionMixer.SetInputWeight(LOCOMOTION_STEP_FORWARD_LEFT_ANIMATION_INDEX, stepType == CharacterLocomotionAnimationState.StepForwardLeft ? step : 0);
            _locomotionMixer.SetInputWeight(LOCOMOTION_STEP_BACKWARD_RIGHT_ANIMATION_INDEX, stepType == CharacterLocomotionAnimationState.StepBackwardRight ? step : 0);
            _locomotionMixer.SetInputWeight(LOCOMOTION_STEP_BACKWARD_LEFT_ANIMATION_INDEX, stepType == CharacterLocomotionAnimationState.StepBackwardLeft ? step : 0);
        }
        private void SetLocomotionJobStepWeight(float stepWeight)
        {
            var job = _locomotionAnimationPlayable.GetJobData<LocomotionAnimationJob>();
            
            if (_isCurrentFootRight)
            {
                job.RightFootStepWeight = Mathf.Clamp01(stepWeight);
            }
            else
            {
                job.LeftFootStepWeight = Mathf.Clamp01(stepWeight);
            }
            
            _locomotionAnimationPlayable.SetJobData(job);
        }
        
        private (AnimationClipPlayable clipPlayable, CharacterLocomotionAnimationState stepType) GetStepToIdleInfo()
        {
            if (_steps > 0)
            {
                return _steps % 2 == 1 ?
                    (_locomotionStepBackwardRightClipPlayable, CharacterLocomotionAnimationState.StepBackwardRight) :
                    (_locomotionStepBackwardLeftClipPlayable, CharacterLocomotionAnimationState.StepBackwardLeft);
            }
            else
            {
                return _steps % 2 == -1 ?
                    (_locomotionStepForwardLeftClipPlayable, CharacterLocomotionAnimationState.StepForwardLeft) :
                    (_locomotionStepForwardRightClipPlayable, CharacterLocomotionAnimationState.StepForwardRight);
            }
        }
        
        private void SetFootIKWeight(float weight)
        {
            rightFootIK.weight = weight;
        }
        private void SampleStrikeEnd(bool useStep, out Vector3 weaponPos, out Quaternion weaponRot)
        {
            ResetSampler();
            
            var time = Mathf.Max(
                _samplePolearmStrikeClipPlayable.GetAnimationClip().length,
                _sampleLocomotionStepClipPlayable.GetAnimationClip().length);
            
            _sampleMixer.SetInputWeight(0, useStep ? 0 : 1);
            _sampleMixer.SetInputWeight(1, useStep ? 1 : 0);
            _sampleMixer.SetInputWeight(2, 0);
            _sampleMixer.SetInputWeight(3, 1);
            _sampleMixer.SetSpeed(1);
            _samplerGraph.Evaluate(time);

            weaponPos = samplerWeaponBone.position;
            weaponRot = samplerWeaponBone.rotation;
        }
        
        private void ResetSampler()
        {
            samplerAnimator.gameObject.transform.position = _defaultSamplerPosition;
            _samplePolearmStrikeClipPlayable.SetTime(0);
            _sampleLocomotionStepClipPlayable.SetTime(0);
            
            _sampleMixer.SetInputWeight(0, 1);
            _sampleMixer.SetInputWeight(1, 0);
            _sampleMixer.SetInputWeight(2, 1);
            _sampleMixer.SetInputWeight(3, 0);
            _sampleMixer.SetSpeed(1);
            _samplerGraph.Evaluate(0);
        }

        public void ManualDestroy()
        {
            if (_graph.IsValid())
            {
                _graph.Destroy();
            }
            
            if (_samplerGraph.IsValid())
            {
                _samplerGraph.Destroy();
            }
        }
    }
}