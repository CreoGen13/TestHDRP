using System.Collections.Generic;
using Infrastructure.UtilityMonoBehaviour;
using UnityEngine;

namespace AnimationBalanceSystem
{
    public class CenterOfMassView : MonoBehaviour
    {
        [SerializeField] private List<CenterOfMassSegmentView> segments;
        [SerializeField] private CenterOfMassWeaponView weapon;
        
        [SerializeField] private Transform projectionPlane;
        
        [SerializeField] private Transform centerOfMassGizmo;
        [SerializeField] private Transform centerOfMassProjectionGizmo;
        
        [SerializeField] private Transform leftFootStart;
        [SerializeField] private Transform leftFootEnd;
        [SerializeField] private Transform rightFootStart;
        [SerializeField] private Transform rightFootEnd;
        [SerializeField] private DebugCircleView debugCircleView;
        
        private float _radius;

        private void Update()
        {
            var centerOfMass = GetCenterOfMass();
            centerOfMassGizmo.position = centerOfMass.position;
            centerOfMassProjectionGizmo.position = centerOfMass.projection;
            
            var supportArea = GetSupportArea();
            debugCircleView.transform.position = supportArea.position;
            debugCircleView.ChangeRadius(supportArea.radius);
        }

        private (Vector3 position, Vector3 projection) GetCenterOfMass()
        {
            var weightedSum = Vector3.zero;
            var totalMass = 0f;
            
            foreach (var segment in segments)
            {
                var segmentCom = segment.GetCenterOfMass();
                
                weightedSum += segmentCom * segment.PercentOfMass;
                totalMass += segment.PercentOfMass;
            }
            
            weightedSum += weapon.GetCenterOfMass() * weapon.Mass;
            totalMass += weapon.Mass;

            var position = weightedSum / totalMass;
            var projection = new Vector3(position.x, projectionPlane.position.y, position.z);
            
            return (position, projection);
        }
        
        private (Vector3 position, float radius) GetSupportArea()
        {
            var leftFootPosition = Vector3.Lerp(leftFootStart.position, leftFootEnd.position, 0.5f);
            var rightFootPosition = Vector3.Lerp(rightFootStart.position, rightFootEnd.position, 0.5f);
            var position = Vector3.Lerp(leftFootPosition, rightFootPosition, 0.5f);
            var finalPosition = new Vector3(position.x, transform.position.y, position.z);
            var radius = (leftFootPosition - rightFootPosition).magnitude / 2;
            
            return (finalPosition, radius);
        }
        
        public bool IsCenterOfMassInsideSupportArea()
        {
            var centerOfMass = GetCenterOfMass();
            var supportArea = GetSupportArea();
            
            return Vector3.Distance(centerOfMass.projection, supportArea.position) <= supportArea.radius;
        }
    }
}