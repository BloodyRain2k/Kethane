using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Kethane
{
    public class KethaneDetector : PartModule
    {
        [KSPField(isPersistant = false)]
        public float DetectingPeriod = 1.2f;

        [KSPField(isPersistant = false)]
        public float DetectingHeight = 300000;

        [KSPField(isPersistant = false)]
        public float PowerConsumption = 0.8f;

        [KSPField(isPersistant = false)]
        public string BaseTransform;

        [KSPField(isPersistant = false)]
        public string PartTransform;

        [KSPField(isPersistant = false)]
        public string HeadingTransform;

        [KSPField(isPersistant = false)]
        public string ElevationTransform;

        [KSPField]
        public bool IsDetecting;

        private double TimerEcho;

        private float powerRatio;

        private static AudioSource PingEmpty;
        private static AudioSource PingDeposit;

        [KSPEvent(guiActive = true, guiName = "Activate Detector", active = true)]
        public void EnableDetection()
        {
            IsDetecting = true;
        }

        [KSPEvent(guiActive = true, guiName = "Deactivate Detector", active = false)]
        public void DisableDetection()
        {
            IsDetecting = false;
        }

        [KSPAction("Activate Detector")]
        public void EnableDetectionAction(KSPActionParam param)
        {
            EnableDetection();
        }

        [KSPAction("Deactivate Detector")]
        public void DisableDetectionAction(KSPActionParam param)
        {
            DisableDetection();
        }

        [KSPAction("Toggle Detector")]
        public void ToggleDetectionAction(KSPActionParam param)
        {
            IsDetecting = !IsDetecting;
        }

        [KSPAction("Show Map")]
        public void ShowMapAction(KSPActionParam param)
        {
            ShowMap();
        }

        [KSPAction("Hide Map")]
        public void HideMapAction(KSPActionParam param)
        {
            HideMap();
        }

        [KSPAction("Toggle Map")]
        public void ToggleMapAction(KSPActionParam param)
        {
            KethaneController.GetInstance(this.vessel).ShowDetectorWindow = !KethaneController.GetInstance(this.vessel).ShowDetectorWindow;
        }

        [KSPEvent(guiActive = true, guiName = "Show Map", active = true)]
        public void ShowMap()
        {
            KethaneController.GetInstance(this.vessel).ShowDetectorWindow = true;
        }

        [KSPEvent(guiActive = true, guiName = "Hide Map", active = false)]
        public void HideMap()
        {
            KethaneController.GetInstance(this.vessel).ShowDetectorWindow = false;
        }

        [KSPField(isPersistant = false, guiActive = true, guiName = "Status")]
        public string Status;

        [KSPEvent(guiActive = true, guiName = "Re-Gen", active = true)]
        public void ReGen()
        {
            var controller = KethaneController.GetInstance(this.vessel);
            var sc = new System.Diagnostics.Stopwatch();
            sc.Start();
            while (controller.GetDepositUnder() == null && sc.ElapsedMilliseconds <= 5000) {
                controller.GenerateKethaneDeposits();
            }
        }

        public override string GetInfo()
        {
            return String.Format("Maximum Altitude: {0:N0}m\nPower Consumption: {1:F2}/s\nScanning Period: {2:F2}s", DetectingHeight, PowerConsumption, DetectingPeriod);
        }

        public override void OnStart(PartModule.StartState state)
        {
            if (state == StartState.Editor) { return; }
            this.part.force_activate();
            #region Sound effects
            PingEmpty = gameObject.AddComponent<AudioSource>();
            WWW wwwE = new WWW("file://" + KSPUtil.ApplicationRootPath.Replace("\\", "/") + "PluginData/mmi_kethane/sounds/echo_empty.wav");
            if ((PingEmpty != null) && (wwwE != null))
            {
                PingEmpty.clip = wwwE.GetAudioClip(false);
                PingEmpty.volume = 1;
                PingEmpty.Stop();
            }

            PingDeposit = gameObject.AddComponent<AudioSource>();
            WWW wwwD = new WWW("file://" + KSPUtil.ApplicationRootPath.Replace("\\", "/") + "PluginData/mmi_kethane/sounds/echo_deposit.wav");
            if ((PingDeposit != null) && (wwwD != null))
            {
                PingDeposit.clip = wwwD.GetAudioClip(false);
                PingDeposit.volume = 1;
                PingDeposit.Stop();
            }
            #endregion
        }

        public override void OnUpdate()
        {
            Events["EnableDetection"].active = !IsDetecting;
            Events["DisableDetection"].active = IsDetecting;
            var controller = KethaneController.GetInstance(this.vessel);
            Events["ShowMap"].active = !controller.ShowDetectorWindow;
            Events["HideMap"].active = controller.ShowDetectorWindow;

            if (Misc.GetTrueAltitude(vessel) <= this.DetectingHeight)
            {
                if (IsDetecting)
                {
                    Status = powerRatio > 0 ? "Active" : "Insufficient Power";
                }
                else
                {
                    Status = "Idle";
                }
            }
            else
            {
                Status = "Out Of Range";
            }

            CelestialBody body = this.vessel.mainBody;
            if (body == null)
                return;

            var BaseT = this.part.transform.FindChild("model");

            if (!String.IsNullOrEmpty(PartTransform))
            {
                BaseT = BaseT.FindChild(PartTransform);
            }

            BaseT = BaseT.FindChild(BaseTransform);

            Vector3 bodyCoords = BaseT.InverseTransformPoint(body.transform.position);
            Vector2 pos = Misc.CartesianToPolar(bodyCoords);

            var alpha = (float)Misc.NormalizeAngle(pos.x + 90);
            var beta = (float)Misc.NormalizeAngle(pos.y);

            Transform RotH = BaseT.FindChild(HeadingTransform);
            Transform RotV = RotH.FindChild(ElevationTransform);

            if (Math.Abs(RotH.localEulerAngles.y - beta) > 90)
            {
                beta += 180;
                alpha = 360 - alpha;
            }

            var speed = Time.deltaTime * this.powerRatio * 60;

            RotH.localRotation = Quaternion.RotateTowards(RotH.localRotation, Quaternion.AngleAxis(beta, new Vector3(0, 1, 0)), speed);
            RotV.localRotation = Quaternion.RotateTowards(RotV.localRotation, Quaternion.AngleAxis(alpha, new Vector3(1, 0, 0)), speed);

            if (float.IsNaN(RotH.localRotation.w)) { RotH.localRotation = Quaternion.identity; }
            if (float.IsNaN(RotV.localRotation.w)) { RotV.localRotation = Quaternion.identity; }
        }

        public override void OnFixedUpdate()
        {
        	var controller = KethaneController.GetInstance(this.vessel);
        	double Altitude = Misc.GetTrueAltitude(vessel);
        	if (IsDetecting && this.vessel != null && this.vessel.gameObject.activeSelf && Altitude <= this.DetectingHeight)
        	{
        		var energyRequest = PowerConsumption * TimeWarp.fixedDeltaTime;
        		var energyDrawn = this.part.RequestResource("ElectricCharge", energyRequest);
        		this.powerRatio = energyDrawn / energyRequest;
        		TimerEcho += Time.deltaTime * (1 + Math.Log(TimeWarp.CurrentRate)) * this.powerRatio;

        		var TimerThreshold = this.DetectingPeriod + Altitude * 0.000005d; // 0,5s delay at 100km

        		if (TimerEcho >= TimerThreshold)
        		{
        			var ping = 0;
        			
        			var variation = Math.Sqrt(Math.Pow(Altitude / Math.Cos(5 * Math.PI / 180), 2) - Math.Pow(Altitude, 2)); // variation in meter?
        			variation = variation / (vessel.mainBody.Radius * 2 * Math.PI) * 360; // variation in planetary degree?
        			//                		print("V: " + variation.ToString("F5"));
        			//                		variation = 10;
        			
        			var DepositUnder = controller.GetDepositUnder();
        			if (DepositUnder != null && DepositUnder.Kethane >= 1.0f)
        			{
        				controller.DrawMap(true);
        				controller.LastLat = vessel.latitude;
        				controller.LastLon = Misc.clampDegrees(vessel.longitude);
        				controller.LastQuantity = DepositUnder.Kethane;
        			}
        			else
        			{
        				controller.DrawMap(false);
        			}
        			
        			for (int i = 0; i < 8; i++) {
        				var lat = Math.Cos(Math.PI / 4 * i) * variation;
        				var lon = Math.Sin(Math.PI / 4 * i) * variation;
        				
        				DepositUnder = controller.GetDepositUnder(lat, lon);
        				if (DepositUnder != null && DepositUnder.Kethane >= 1.0f)
        				{
        					controller.DrawMap(true, lat, lon);
        					controller.LastLat = vessel.latitude + lat;
        					controller.LastLon = Misc.clampDegrees(vessel.longitude + lon);
        					controller.LastQuantity = DepositUnder.Kethane;
        				}
        				else
        				{
        					controller.DrawMap(false, lat, lon);
        				}
        			}
        			
        			if (vessel == FlightGlobals.ActiveVessel && controller.ScanningSound) {
        				if (ping == 0) {
        					PingEmpty.Play();
        				} else {
        					PingDeposit.Play();
        				}
        			}
        			
        			TimerEcho = 0;
        		}
        	}
        	else
        	{
        		this.powerRatio = 0;
        	}
        }

        public override void OnSave(ConfigNode node)
        {
            KethaneController.GetInstance(this.vessel).SaveAndLoadState();
        }
    }
}
