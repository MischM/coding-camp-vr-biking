using UnityEngine;
using System;

namespace rayzngames
{
	[RequireComponent(typeof(Rigidbody))]
	public class BicycleVehicle : MonoBehaviour
	{
		//Inputs, Getters, and setters
		/// <summary>
		/// Recieves the input for Steering. 
		/// </summary>
		public float horizontalInput { get; set; }
		/// <summary>
		/// Recieves the input for Accelerating
		/// </summary>
		public float verticalInput { get; set; }
		/// <summary>
		/// Recieves the input for Braking 
		/// </summary>
		public bool braking { get; set; }
		/// <summary>
		/// Returns the current control state of the vehicle 
		/// </summary>
		public bool isInControl { get; private set; }
		/// <summary>
		/// The surrent slip state of the front wheel (True or false)
		/// </summary>
		public bool slipFront { get; private set; }
		/// <summary>
		/// The current slip state of the rear wheel (True or false)
		/// </summary>
		public bool slipRear{ get; private set; }
		
		Rigidbody rb;
		/// <summary>
		/// The force applied to the rear wheel when verticalinput si provided / is 0 when braking
		/// </summary>
		[Header("Power/Braking")]
		[Space(5)]
		[Tooltip("The force applied to the rear wheel when verticalinput si provided / is 0 when braking")]
		public float motorForce = 50f;
		/// <summary>
		/// The MAX braking force applied when braking input is provided and brakepower is 1
		/// </summary>
		[Tooltip("The maximum braking force applied when braking input is provided and brakepower is 1")]
		public float brakeForce = 100;
		/// <summary>
		/// The braking power used on the front wheel, the moment braking is enabled. (0 = no braking / 1 = brakeforce)"
		/// </summary>
		[Tooltip("The braking power used on the front wheel, the moment braking is enabled. (0 = no braking / 1 = brakeforce)")]
		[Range(0, 1)] public float frontBrakePower = 1f;
		/// <summary>
		/// The braking power used on the rear wheel, the moment braking is enabled. (0 = no braking / 1 = brakeforce)
		/// </summary>
		[Tooltip("The braking power used on the rear wheel, the moment braking is enabled. (0 = no braking / 1 = brakeforce)")]
		[Range(0, 1)] public float rearBrakePower = 1f;
		/// <summary>
		/// Adjust this paameter to add an offset to the center of gravity of the vehicle
		/// </summary>
		[Tooltip("Adjust this paameter to add an offset to the center of gravity")]
		public Vector3 COG;

		/// <summary>
		/// Defines the maximum steering angle for the bike, when horizontal input is provided
		/// </summary>
		[Space(20)]		
		[Header("Steering")]
		[Space(5)]		
		[Tooltip("Defines the maximum steering angle for the bike, when horizontal input is provided")]
		[SerializeField] float maxSteeringAngle = 45f;

		/// <summary>
		/// Defines the highest possisble degrees of Steering at 30m/s (108 km/h) use a low value
		/// </summary>
		[Tooltip("Defines the highest possisble degrees of Steering at 30m/s (108 km/h) use a low value")]
		[SerializeField] float minSteeringAngle = 5f;
		/// <summary>
		/// Sets current_MaxSteering by interpolating Min and Max steering parameters based on the speed of the RB up to 108km/h  (0 - No effect) (1 - Full)
		/// </summary>
		[Tooltip("Sets how current_MaxSteering is reduced based on the speed of the RB, (0 - No effect) (1 - Full)")]
		[Range(0f, 1f)][SerializeField] float steerReductorAmmount;
		/// <summary>
		/// Sets the Steering sensitivity or [Steering Stiffness] 0 - Stiff (Unresposive), 1 - Soft (Quick respose)
		/// /// </summary>
		[Tooltip("Sets the Steering sensitivity [Steering Stiffness] 0 - No turn, 1 - SensitiveTurn)")]
		[Range(0.001f, 1f)][SerializeField] float turnSmoothing;

		/// <summary>
		/// Defines the maximum leaning angle when steering in this vehicle
		/// </summary>
		[Space(20)]
		[Header("Lean")]
		[Space(5)]
		[Tooltip("Defines the maximum leaning angle when turning for this bike")]
		[SerializeField] float maxLeanAngle = 45f;
		/// <summary>
		/// Sets the Leaning sensitivity (0 - None, 1 - full
		/// </summary>
		[Tooltip("Sets the Leaning sensitivity (0 - None, 1 - full")]
		[Range(0.001f, 1f)][SerializeField] float leanSmoothing;
		private protected float targetLeanAngle;
		
		/// <summary>
		/// The handle of the vehicle
		/// </summary>
		[Space(20)]
		[Header("Object References")]
		public Transform handle;
		[Space(10)]
		[SerializeField] WheelCollider frontWheel;
		[SerializeField] WheelCollider rearWheel;
		[Space(10)]
		[SerializeField] Transform frontWheelTransform;
		[SerializeField] Transform rearWheelTransform;
		TrailRenderer frontTrail;
		TrailRenderer rearTrail;
		[Space(10)]
		[Tooltip("Drop here the Front Trail Prefab")]
		[SerializeField] ContactProvider frontTrailContact;
		[Tooltip("Drop here the Rear Trail Prefab")]
		[SerializeField] ContactProvider rearTrailContact;
		private ParticleSystem frontSmoke;
		private ParticleSystem rearSmoke;

		[Space(10)]
		public ContactProvider controlContact;
		/// <summary>
		/// The actual Steering angle in use
		/// </summary>
		[Header("Info")]
		[Tooltip("The actual Steering angle in use")]		
		public float currentSteeringAngle { get; private set; }
		/// <summary>
		/// Dynamic MAX steering angle based on the linearspeed of the RB, affected by sterReductorAmmount
		/// </summary>
		[Tooltip("Dynamic MAX steering angle based on the linearspeed of the RB, affected by sterReductorAmmount")]
		public float current_maxSteeringAngle { get; private set; }
		/// <summary>
		/// The current lean angle applied
		/// </summary>
		[Tooltip("The current lean angle applied")]
		[Range(-45, 45)] public float currentLeanAngle { get; private set; }
		/// <summary>
		/// The current vehicle speed in M/s
		/// </summary>
		[Header("Speed M/s")]
		public float currentSpeed { get; private set; }
		protected private WheelHit frontInfo;
		protected private WheelHit rearInfo;
		
		void Awake()
		{
			rb = GetComponent<Rigidbody>();
			InControl(true);
		}
		void Start()
		{
			if (frontTrailContact != null)
			{
				frontTrail = frontTrailContact.transform.GetChild(0).GetComponent<TrailRenderer>();
				frontSmoke = frontTrailContact.transform.GetChild(1).GetComponent<ParticleSystem>();
			}
			else { Debug.LogWarning("Easy Bike WARNING: \n Front TrailsContact prefab has not been assigned Slip Trails and Smoke will not appear"); }
			if (rearTrailContact != null)
			{
				rearTrail = rearTrailContact.transform.GetChild(0).GetComponent<TrailRenderer>();
				rearSmoke = rearTrailContact.transform.GetChild(1).GetComponent<ParticleSystem>();
			}
			else {Debug.LogWarning ("Easy Bike WARNING: \n Rear TrailsContact prefab has not been assigned  Slip Trails and Smoke will not appear"); }

			//To stop bike from Jittering
			frontWheel.ConfigureVehicleSubsteps(5, 12, 15);
			rearWheel.ConfigureVehicleSubsteps(5, 12, 15);
		}


		// Update is called once per frame
		void FixedUpdate()
		{
			if (isInControl)
			{
				HandleEngine();				
				HandleSteering();
				LeanOnTurnLocal();
				UpdateHandles();
			}
			UpdateWheels();
			EmitTrail();
			Speed_O_Meter();			
		}
		/// <summary>
		/// Sets the Incontrol state of the vehicle allowing it to recieve inputs
		/// </summary>
		/// <param name="state">True or False</param>
		public void InControl(bool state)
		{
			if (isInControl != state)
			{
				isInControl = state;				
			}
		}
		/// <summary>
		/// Constrains the rotation of the RB of the bicycle on the Z component when set to true
		/// </summary>
		/// <param name="state"> Enables disables the Z rotation constraint </param>
		public void ConstrainRotation(bool state)
		{
			if (state == true)
			{
				rb.constraints = RigidbodyConstraints.FreezeRotationZ;
			}
			else
			{
				rb.constraints = RigidbodyConstraints.None;
			}
		}
		/// <summary>
		/// Returns wether the requested vehicle is on the ground 
		/// </summary>
		/// <returns> True or False</returns>
		public bool OnGround()
		{
			return controlContact.GetContact();
		}
		
		private void HandleEngine()
		{
			rearWheel.motorTorque = braking ? 0f : verticalInput * motorForce;
			float force = braking ? brakeForce : 0f;
			ApplyBraking(force);
		}
		private void ApplyBraking(float brakeForce)
		{
			frontWheel.brakeTorque = brakeForce * frontBrakePower;
			rearWheel.brakeTorque = brakeForce * rearBrakePower;
		}

		//This replaces the (Magic numbers) that controlled an exponential decay function for maxteeringAngle (maxSteering angle was not adjustable)
		//This one allows to customize Default bike maxSteeringAngle parameters and maxSpeed allowing for better scalability for each vehicle	
		private void MaxSteeringReductor()
		{
			//(30 = 108 kmh) is the value at wich currentMaxSteering will be at its minimum,			
			float t = (rb.linearVelocity.magnitude / 30) * steerReductorAmmount;
			t = t > 1 ? 1 : t;
			current_maxSteeringAngle = Mathf.LerpAngle(maxSteeringAngle, minSteeringAngle, t);
		}
		private void HandleSteering()
		{
			MaxSteeringReductor();
			currentSteeringAngle = Mathf.Lerp(currentSteeringAngle, current_maxSteeringAngle * horizontalInput, turnSmoothing * 0.1f);
			frontWheel.steerAngle = currentSteeringAngle;
			//We invert Input for rotating in the correct direction
			targetLeanAngle = maxLeanAngle * -horizontalInput;
		}
		private void UpdateHandles()
		{
			float currentY = handle.localEulerAngles.y;
			float delta = Mathf.DeltaAngle(currentY, currentSteeringAngle);
			handle.Rotate(Vector3.up, delta, Space.Self);
		}

		private void LeanOnTurnLocal()
		{
			Vector3 currentRot = transform.localEulerAngles;
			//Case: not moving much		
			if (rb.linearVelocity.magnitude < 2f)
			{
				currentLeanAngle = Mathf.LerpAngle(currentRot.z, 0f, 0.05f);
			}
			//Case: Not steering or steering a tiny amount
			if (currentSteeringAngle < 0.5f && currentSteeringAngle > -0.5)
			{
				currentLeanAngle = Mathf.LerpAngle(currentRot.z, 0f, leanSmoothing * 0.1f);
			}
			//Case: Steering
			else
			{
				currentLeanAngle = Mathf.LerpAngle(currentLeanAngle, targetLeanAngle, leanSmoothing * 0.1f);
				rb.centerOfMass = new Vector3(rb.centerOfMass.x, COG.y, rb.centerOfMass.z);
			}
			transform.localRotation = Quaternion.Euler(currentRot.x, currentRot.y, currentLeanAngle);
		}

		private void UpdateWheels()
		{
			//Front wheel is handled individually
			UpdateFrontWheel();
			UpdateSingleWheel(rearWheel, rearWheelTransform);
		}

		protected private float frontSpin;
		void UpdateFrontWheel()
		{
			GetWheelSpin();
			frontWheel.GetWorldPose(out Vector3 pos, out Quaternion spinRotation);
			frontWheelTransform.position = pos;
			spinRotation = Quaternion.Euler(frontSpin, 0f, 0f);
			frontWheelTransform.localRotation = spinRotation;
		}

		void GetWheelSpin()
		{
			// Convert rpm to degrees per second
			float degreesPerSecond = frontWheel.rpm * 6f; // 360 degrees / 60 seconds = 6														  
			frontSpin += degreesPerSecond * Time.fixedDeltaTime;// Integrate over fixedeltaTIme
			// Wrap 0–360
			if (frontSpin >= 360f) frontSpin = 0f;
			if (frontSpin < 0f) frontSpin = 360f;
		}

		private void EmitTrail()
		{
			//Slip dependandt trails and particles.
			frontWheel.GetGroundHit(out frontInfo);
			rearWheel.GetGroundHit(out rearInfo);
			//Slip Trails Side
			rearSideCoeff = rearInfo.sidewaysSlip / rearWheel.sidewaysFriction.extremumSlip;
			frontSideCoeff = frontInfo.sidewaysSlip / frontWheel.sidewaysFriction.extremumSlip;
			//FWD
			frontFWD = frontInfo.forwardSlip / frontWheel.forwardFriction.extremumSlip;
			rearFWD = rearInfo.forwardSlip / rearWheel.forwardFriction.extremumSlip;	

			if (frontTrailContact != null) //We can get contacts
			{
				slipFront = SlipFront();
				if (slipFront)
				{
					frontTrail.emitting = true;
					if (!frontSmoke.isPlaying) { frontSmoke.Play(); }
				}
				else
				{
					frontTrail.emitting = false;
					if (frontSmoke.isPlaying) { frontSmoke.Stop(); }
				}			
			}
			//We can get contacts
			if (rearTrailContact != null)
			{
				slipRear = SlipRear();
				if (slipRear)
				{
					rearTrail.emitting = true;
					if (!rearSmoke.isPlaying) { rearSmoke.Play(); }
				}
				else
				{
					rearTrail.emitting = false;
					if (rearSmoke.isPlaying) { rearSmoke.Stop(); }
				}
			}
		}
		bool SlipFront()
		{
			if (frontTrailContact.GetContact())
			{
				if (frontFWD > 1.1f || frontFWD < -1.1f || frontSideCoeff > 1.1f || frontSideCoeff < -1.1f)
				{
					return true;
				}
				else
				{
					return false;					
				}
			}
			else
			{
				return false;
			}			
		 }

		bool SlipRear()
		{
			if (rearTrailContact.GetContact())
			{
				if (rearFWD > 1.1f || rearFWD < -1.1f || rearSideCoeff > 1.1f || rearSideCoeff < -1.1f)
				{
					return true;
				}
				else
				{
					return false;				
				}
			}
			else
			{
				return false;
			}
		}
		private void UpdateSingleWheel(WheelCollider wheelCollider, Transform wheelTransform)
		{
			Vector3 position;
			Quaternion rotation;
			wheelCollider.GetWorldPose(out position, out rotation);
			wheelTransform.rotation = rotation;
			wheelTransform.position = position;
		}

		void Speed_O_Meter()
		{
			currentSpeed = rb.linearVelocity.magnitude;
		}

		#region  Extra Setup Functions

		[ContextMenu("Set Wheels Springs and Frictions (Unity Default)")]
		public void DefaultSuspensionParameters()
		{
			rb = GetComponent<Rigidbody>();
			JointSpring susSpring;

			//Front
			susSpring.spring = 23.33f * rb.mass;
			susSpring.damper = 3 * rb.mass;
			susSpring.targetPosition = frontWheel.suspensionSpring.targetPosition;

			frontWheel.suspensionSpring = susSpring;
			//rear			
			susSpring.spring = 23.33f * rb.mass;
			susSpring.damper = 3 * rb.mass;
			susSpring.targetPosition = rearWheel.suspensionSpring.targetPosition;

			rearWheel.suspensionSpring = susSpring;			

			FrictionParameters_Motorbike();			
		}
		[ContextMenu("Set Wheels Springs and Frictions (Motorbike)")]
		public void MotorbikeSuspensionParameters()
		{
			rb = GetComponent<Rigidbody>();
			JointSpring susSpring;

			//Front
			susSpring.spring = 750f * rb.mass;
			susSpring.damper = 32.5f * rb.mass;
			susSpring.targetPosition = frontWheel.suspensionSpring.targetPosition;
			frontWheel.suspensionSpring = susSpring;

			//rear			
			susSpring.spring = 350f * rb.mass;
			susSpring.damper = 22.5f * rb.mass;
			susSpring.targetPosition = rearWheel.suspensionSpring.targetPosition;
			rearWheel.suspensionSpring = susSpring;

			//Frictions
			FrictionParameters_Motorbike();
		}
		[ContextMenu("Set Wheels Springs and Frictions (Bicycle)")]
		public void BicycleSuspensionParameters()
		{
			rb = GetComponent<Rigidbody>();
			JointSpring susSpring;

			//Front
			susSpring.spring = 256 * rb.mass;
			susSpring.damper = 16f * rb.mass;
			susSpring.targetPosition = frontWheel.suspensionSpring.targetPosition;
			frontWheel.suspensionSpring = susSpring;

			//rear			
			susSpring.spring = 219 * rb.mass;
			susSpring.damper = 11.7f * rb.mass;
			susSpring.targetPosition = rearWheel.suspensionSpring.targetPosition;
			rearWheel.suspensionSpring = susSpring;

			//Frictions
			FrictionParameters_Bicycle();
		}
		
		public void FrictionParameters_Motorbike()
		{
			WheelFrictionCurve frontForwardFrictionCurve = CreateFrictionCurve(0.3f, 1.25f, 0.5f, 1f, 1f);
			WheelFrictionCurve frontSidewaysFrictionCurve = CreateFrictionCurve(0.35f, 1.56f, 0.5f, 1f, 1f);
			frontWheel.forwardFriction = frontForwardFrictionCurve;
			frontWheel.sidewaysFriction = frontSidewaysFrictionCurve;

			WheelFrictionCurve rearForwardFrictionCurve = CreateFrictionCurve(0.15f, 2.25f, 0.5f, 1f, 1f);
			WheelFrictionCurve rearSidewaysFrictionCurve = CreateFrictionCurve(0.3f, 2.15f, 0.5f, 1f, 1f);
			rearWheel.forwardFriction = rearForwardFrictionCurve;
			rearWheel.sidewaysFriction = rearSidewaysFrictionCurve;
		}
		public void FrictionParameters_Bicycle()
		{
			WheelFrictionCurve frontForwardFrictionCurve = CreateFrictionCurve(0.3f, 1f, 0.5f, 1f, 1f);
			WheelFrictionCurve frontSidewaysFrictionCurve = CreateFrictionCurve(0.35f, 1.25f, 0.5f, 1f, 1);
			frontWheel.forwardFriction = frontForwardFrictionCurve;
			frontWheel.sidewaysFriction = frontSidewaysFrictionCurve;

			WheelFrictionCurve rearForwardFrictionCurve = CreateFrictionCurve(0.15f, 2.5f, 0.6f, 1.75f, 1);
			WheelFrictionCurve rearSidewaysFrictionCurve = CreateFrictionCurve(0.3f, 2.25f, 0.6f, 1.75f, 1);
			rearWheel.forwardFriction = rearForwardFrictionCurve;
			rearWheel.sidewaysFriction = rearSidewaysFrictionCurve;
		}

		private WheelFrictionCurve CreateFrictionCurve(float exSlip, float exValue, float asSlip, float asValue, float stiffness)
		{
			WheelFrictionCurve frictionCurve = new WheelFrictionCurve();
			frictionCurve.asymptoteSlip = asSlip;
			frictionCurve.asymptoteValue = asValue;
			frictionCurve.extremumSlip = exSlip;
			frictionCurve.extremumValue = exValue;
			frictionCurve.stiffness = stiffness;
			return frictionCurve;
		}

		protected private float rearSideCoeff;
		protected private float frontSideCoeff;
		float frontFWD;
		float rearFWD;
		/// <summary>
		/// Resultant values are normalized slip values that go from (≈ 0.0 – 0.5 - normal grip below extremium slip) 
		/// (to 1 - Peak of grip limit,right at eXtremium slip) (>1 - slip slide, grip is falling toward the asymptote) 
		/// </summary>
		public void WheelNormalizedSlipInfo()
		{
			frontWheel.GetGroundHit(out frontInfo);
			rearWheel.GetGroundHit(out rearInfo);

			rearSideCoeff = rearInfo.sidewaysSlip / rearWheel.sidewaysFriction.extremumSlip;
			frontSideCoeff = frontInfo.sidewaysSlip / frontWheel.sidewaysFriction.extremumSlip;

			frontFWD = frontInfo.forwardSlip / frontWheel.forwardFriction.extremumSlip;
			rearFWD = rearInfo.forwardSlip / rearWheel.forwardFriction.extremumSlip;

			string frontGrip = "";
			string rearGrip = "";
			//Front Grip/Slip
			if (frontFWD < 1.1f && frontFWD > -1.1f || frontSideCoeff < 1.1f && frontSideCoeff > -1.1f)
			{
				frontGrip = "Grip";
			}
			if (frontFWD > 1.1f || frontFWD < -1.1f || frontSideCoeff > 1.1f || frontSideCoeff < -1.1f) 
			{
				frontGrip = "Slip"; 
			}
			//Rear Grip/Slip
			if (rearFWD < 1.1f & rearFWD > -1.1f || rearSideCoeff < 1.1f && rearSideCoeff > -1.1f)
			{
				rearGrip = "Grip";
			}
			if (rearFWD > 1.1f || rearFWD < -1.1f || rearSideCoeff > 1.1f || rearSideCoeff < -1.1f) 
			{ 
				rearGrip = "Slip"; 
			}
		
			Debug.Log("Rear Coeficient = " + rearSideCoeff + " // " + " Front Coeficient = " + rearSideCoeff);
			Debug.Log("Rear Grip = " + rearGrip + "// " + " Front Grip = " + frontGrip);
		}

		#endregion
	}
}