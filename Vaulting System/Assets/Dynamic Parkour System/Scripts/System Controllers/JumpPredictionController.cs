﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace Climbing
{
    public class JumpPredictionController : MonoBehaviour
    {
        [SerializeField]
        public float maxHeight = 1.5f;
        [SerializeField]
        public float maxDistance = 5.0f;
        [SerializeField]
        public float maxTime = 2.0f;

        float turnSmoothVelocity;
        ThirdPersonController controller;

        Vector3 origin;
        Vector3 target;
        float distance = 0;
        float delay = 0;

        public bool showDebug = false;

        bool move = false;
        bool newPoint = false;

        protected float actualSpeed = 0;

        public int accuracy = 50;

        public Point curPoint = null;

        private void Start()
        {
            controller = GetComponent<ThirdPersonController>();
        }

        void OnDrawGizmos()
        {
            if (!showDebug)
                return;

            if (!Application.isPlaying)
                origin = transform.position;

            if (origin == Vector3.zero)
                return;

            //Draw the parabola by sample a few times
            Vector3 lastP = origin;
            for (float i = 0; i < accuracy; i++)
            {
                Vector3 p = SampleParabola(origin, target, maxHeight, i / accuracy);
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(lastP, p);
                Gizmos.DrawWireSphere(p, 0.02f);
                lastP = p;
            }
        }

        public void CheckJump()
        {
            if (hasArrived() && !controller.isJumping && ((controller.isGrounded && curPoint == null) || curPoint != null) && controller.characterMovement.limitMovement)
            {
                if (controller.characterInput.jump && controller.characterInput.movement != Vector2.zero)
                {
                    List<HandlePointsV2> points = new List<HandlePointsV2>();
                    controller.characterDetection.FindAheadPoints(ref points);

                    float minRange = float.NegativeInfinity;
                    float minDist = float.PositiveInfinity;
                    Point p = null;
                    Point fp = curPoint;
                    newPoint = false;

                    //Gets direction relative to the input and the camera
                    Vector3 mov = new Vector3(controller.characterInput.movement.x, 0, controller.characterInput.movement.y);
                    Vector3 inputDir = controller.RotateToCameraDirection(mov) * Vector3.forward;

                    if (showDebug)
                        Debug.DrawLine(transform.position, transform.position + inputDir);

                    if (fp == null)//First Point
                    {
                        RaycastHit hit;
                        if (Physics.Raycast(transform.position, Vector3.down, out hit, 0.5f))
                        {
                            HandlePointsV2 handle = hit.transform.GetComponentInChildren<HandlePointsV2>();
                            if (handle)
                            {
                                for (int i = 0; i < handle.pointsInOrder.Count; i++)
                                {
                                    if (handle.pointsInOrder[i] != null)
                                    {
                                        fp = handle.pointsInOrder[i];
                                        break;
                                    }
                                }
                            }
                        }
                    }

                    foreach (var item in points)
                    {
                        if (item.pointType != PointType.Ledge)
                        {
                            foreach (var point in item.pointsInOrder)
                            {
                                if (point == null || point.type == PointType.Ledge || curPoint == point)
                                    continue;

                                Vector3 targetDirection = point.transform.position - transform.position;

                                Vector3 d1 = new Vector3(targetDirection.x, 0, targetDirection.z);

                                float dot = Vector3.Dot(d1.normalized, inputDir.normalized);

                                if (fp == null)//First Point
                                {
                                    fp = point;
                                }

                                if (fp.transform.parent == point.transform.parent)
                                    continue;

                                if (dot > 0.9 && targetDirection.sqrMagnitude < minDist && minRange - dot < 0.1f )
                                {
                                    p = point;
                                    minRange = dot;
                                    minDist = targetDirection.sqrMagnitude;
                                    newPoint = true;
                                }

                            }
                        }
                    }

                    bool target = false;
                    if (newPoint && p != null)
                    {
                        target = SetParabola(transform.position, p.transform.position);
                        if (target)
                        {
                            curPoint = p;

                            switch (curPoint.type)
                            {
                                case PointType.Pole:
                                    controller.characterMovement.stopMotion = true;
                                    controller.characterAnimation.JumpPrediction(true);
                                    break;
                                case PointType.Ground:
                                    controller.characterMovement.stopMotion = false;
                                    controller.characterAnimation.JumpPrediction(false);
                                    break;
                            }

                            controller.DisableController();
                            controller.isJumping = true;
                        }
                    }
                    
                    if(!target)
                    {
                        Vector3 end = transform.position + inputDir * 4;

                        RaycastHit hit;
                        if(Physics.Raycast(transform.position, inputDir, out hit, 4, controller.characterDetection.climbLayer))
                        {
                            Vector3 temp = hit.point;
                            temp.y = transform.position.y;
                            Vector3 dist = temp - transform.position;

                            if(dist.sqrMagnitude >= 2)
                            {   
                                end = hit.point + hit.normal * (controller.collider2.radius * 2);
                            }
                            else
                            {
                                end = Vector3.zero;
                            }
                        }
                        
                        if(end != Vector3.zero)
                        {
                            //Compute new Jump Point in case of not finding one
                            if(SetParabola(transform.position, end))
                            {
                                controller.characterAnimation.JumpPrediction(false);
                                curPoint = null;
                                controller.characterMovement.stopMotion = true;
                                controller.characterMovement.Fall();
                                controller.DisableController();
                                controller.isJumping = true;
                            }
                        }
                    }

                    points.Clear();
                }
            }
        }

        public bool isMidPoint()
        {
            if (curPoint == null || controller.characterInput.drop) //Player is Droping
            {
                curPoint = null;
                controller.EnableController();
            }
            else if (curPoint)
            {
                if (curPoint.type == PointType.Pole)
                {
                    Vector3 direction = new Vector3(controller.characterInput.movement.x, 0f, controller.characterInput.movement.y).normalized;
                    if (direction != Vector3.zero)
                        controller.RotatePlayer(direction);

                    controller.characterMovement.ResetSpeed();

                    //On MidPoint
                    if (curPoint && !controller.isJumping)
                    {
                        //Delay between allowing new jump
                        if (delay < 0.1f)
                            delay += Time.deltaTime;
                        else
                            CheckJump();

                        return true;
                    }
                }
            }

            return false;
        }

        public void FollowParabola(float length)
        {
            if (move == true)
            {
                actualSpeed += Time.fixedDeltaTime / length;
                if (actualSpeed > 1)
                {
                    actualSpeed = 1;
                }
                controller.characterMovement.rb.position = SampleParabola(origin, target, maxHeight, actualSpeed);

                //Rotate Mesh to Movement
                Vector3 travelDirection = target - origin;
                float targetAngle = Mathf.Atan2(travelDirection.x, travelDirection.z) * Mathf.Rad2Deg;
                float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothVelocity, 0.1f);
                controller.characterMovement.rb.rotation = Quaternion.Euler(0f, angle, 0f);
            }
        }

        public void hasEndedJump()
        {
            if (actualSpeed >= 1.0f && curPoint != null)
            {
                if (!controller.characterMovement.stopMotion)
                    controller.EnableController();

                controller.isJumping = false;
                actualSpeed = 0.0f;
                delay = 0;
                move = false;
            }
            else if (actualSpeed >= 0.7f && curPoint == null) //
            {
                controller.EnableController();
                actualSpeed = 0.0f;
                delay = 0;
                move = false;
            }
        }

        public bool hasArrived()
        {
            return !move;
        }

        public bool SetParabola(Vector3 start, Vector3 end)
        {
            Vector2 a = new Vector2(start.x, start.z);
            Vector2 b = new Vector2(end.x, end.z);
            distance = Vector3.Distance(start, end);

            if (end.y - start.y > maxHeight || (distance > maxDistance))
                return false;

            origin = start;
            target = end;
            move = true;
            newPoint = false;

            actualSpeed = 0.0f;

            return true;
        }

        Vector3 SampleParabola(Vector3 start, Vector3 end, float height, float t)
        {
            float parabolicT = t * 2 - 1;
            Vector3 travelDirection = end - start;
            Vector3 result = start + t * travelDirection;
            result.y += (-parabolicT * parabolicT + 1) * height;

            return result;
        }

    }
}
