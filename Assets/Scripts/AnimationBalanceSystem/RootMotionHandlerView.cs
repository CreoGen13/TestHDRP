using UnityEngine;

namespace AnimationBalanceSystem
{
    public class RootMotionHandlerView : MonoBehaviour
    {
        [SerializeField] private Animator animator;

        private bool _skipNextDelta;
        
        private void Awake()
        {
            animator.applyRootMotion = true;
        }

        private void OnAnimatorMove()
        {
            if (_skipNextDelta)
            {
                _skipNextDelta = false;
                
                return;
            }
            
            transform.position += animator.deltaPosition;
            transform.rotation *= animator.deltaRotation;
        }

        public void ChangeAnimation()
        {
            _skipNextDelta = true;
        }
    }
}