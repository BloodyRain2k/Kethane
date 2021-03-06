﻿using System;
using System.Linq;
using UnityEngine;

namespace Kethane
{
    public class KethaneDrillAnimator : PartModule, IExtractorAnimator
    {
        [KSPField(isPersistant = false)]
        public string DeployAnimation;

        [KSPField(isPersistant = false)]
        public string DrillAnimation;

        [KSPField(isPersistant = false)]
        public string HeadTransform;

        [KSPField(isPersistant = false)]
        public string TailTransform;

        [KSPField(isPersistant = true)]
        public string State;

        private AnimationState[] deployStates;
        private AnimationState[] drillStates;

        private Transform headTransform;
        private Transform tailTransform;

        private KethaneParticleEmitter[] emitters;

        public override void OnStart(PartModule.StartState state)
        {
            deployStates = Misc.SetUpAnimation(DeployAnimation, this.part);
            drillStates = Misc.SetUpAnimation(DrillAnimation, this.part);

            headTransform = this.part.FindModelTransform(HeadTransform);
            tailTransform = this.part.FindModelTransform(TailTransform);

            if (CurrentState == ExtractorState.Deploying) { CurrentState = ExtractorState.Retracted; }
            else if (CurrentState == ExtractorState.Retracting) { CurrentState = ExtractorState.Deployed; }

            if (CurrentState == ExtractorState.Deployed)
            {
                foreach (var deployState in deployStates)
                {
                    deployState.normalizedTime = 1;
                }
            }

            foreach (var drillState in drillStates)
            {
                drillState.enabled = false;
                drillState.wrapMode = WrapMode.Loop;
            }

            if (state == StartState.Editor) { return; }
            if (FlightGlobals.fetch == null) { return; }

            emitters = part.Modules.OfType<KethaneParticleEmitter>().ToArray();

            foreach (var emitter in emitters) {
                emitter.Setup();
                emitter.EmitterTransform.parent = headTransform;
                emitter.EmitterTransform.localRotation = Quaternion.identity;
            }
        }

        public ExtractorState CurrentState
        {
            get
            {
                try
                {
                    return (ExtractorState)Enum.Parse(typeof(ExtractorState), State);
                }
                catch
                {
                    CurrentState = ExtractorState.Retracted;
                    return CurrentState;
                }
            }
            private set
            {
                State = Enum.GetName(typeof(ExtractorState), value);
            }
        }

        public void Deploy()
        {
            if (CurrentState != ExtractorState.Retracted) { return; }
            CurrentState = ExtractorState.Deploying;
            foreach (var state in deployStates)
            {
                state.speed = 1;
            }
        }

        public void Retract()
        {
            if (CurrentState != ExtractorState.Deployed) { return; }
            CurrentState = ExtractorState.Retracting;
            foreach (var state in drillStates)
            {
                state.enabled = false;
                state.normalizedTime = 0;
                state.speed = 0;
            }
            foreach (var state in deployStates)
            {
                state.speed = -1;
            }
        }

        public override void OnUpdate()
        {
            foreach (var deployState in deployStates)
            {
                deployState.normalizedTime = Mathf.Clamp01(deployState.normalizedTime);
            }

            if (CurrentState == ExtractorState.Deploying && deployStates.All(s => s.normalizedTime == 1))
            {
                CurrentState = ExtractorState.Deployed;
                foreach (var state in drillStates)
                {
                    state.enabled = true;
                    state.normalizedTime = 0;
                    state.speed = 1;
                }
            }
            else if (CurrentState == ExtractorState.Retracting && deployStates.All(s => s.normalizedTime == 0))
            {
                CurrentState = ExtractorState.Retracted;
            }

            if (CurrentState != ExtractorState.Retracted)
            {
                RaycastHit hitInfo;
                var hit = raycastGround(out hitInfo);

                foreach (var emitter in emitters.Where(e => e.Label != "gas"))
                {
                    emitter.Emit = hit;
                }
                if (hit)
                {
                    foreach (var emitter in emitters)
                    {
                        emitter.EmitterPosition = headTransform.InverseTransformPoint(hitInfo.point);
                    }
                }

                foreach (var emitter in emitters.Where(e => e.Label == "gas"))
                {
                    if (CurrentState == ExtractorState.Deployed)
                    {
                        emitter.Emit = hit && KethaneController.GetInstance(this.vessel).GetDepositUnder("Kethane") != null;
                    }
                    else
                    {
                        emitter.Emit = false;
                    }
                }
            }
            else
            {
                foreach (var emitter in emitters)
                {
                    emitter.Emit = false;
                }
            }
        }

        public bool CanExtract
        {
            get { return raycastGround(); }
        }

        private bool raycastGround()
        {
            RaycastHit hitInfo;
            return raycastGround(out hitInfo);
        }

        private bool raycastGround(out RaycastHit hitInfo)
        {
            var mask = 1 << FlightGlobals.getMainBody().gameObject.layer;
            var direction = headTransform.position - tailTransform.position;
            return Physics.Raycast(tailTransform.position, direction, out hitInfo, direction.magnitude, mask);
        }
    }
}
