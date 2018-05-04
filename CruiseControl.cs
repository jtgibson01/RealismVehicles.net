using GTA;
using GTA.Native;
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using XInputDotNetPure;
using System.Linq;

namespace RealismVehicles
{

public sealed class CruiseControl : Script
{
	public CruiseControl() {
		Interval = 1;
		Tick += CruiseControlTick;
		Configuration.Load(this);
		if(cruise_control_key != Keys.None) KeyDown += RealismCruiseKeyDown;
		}
	
	///// CRUISE CONTROL /////

	//Speed Hold
	// * For boats, motorcycles, and cars only.
	// * Accelerate to the desired speed.
	// * Once at the desired speed, tap the Cruise Set button (default C) to enable cruise control.
	// * If your vehicle is near cruise control speed, tap the Cancel button (also default C) to turn it off.
	// * If your vehicle is not near cruise control speed, tap the Cruise button again to adjust the set speed.
	// * Cruise control cannot be activated/will automatically deactivate if below the 'minimum speed'.

	//Cancel (Unimplemented)
	// * Tap the brakes, the handbrake, or the Cancel button to turn of f the cruise control system.  The cruise
	//    control will still remember your last speed setting.
	// * The vehicle will also cancel cruise control if a collision is detected.
	
	//Resume Speed
	// * Tap the Resume button (not enabled by default) to return to set speed after the cruise control system has
	//    been shut off.

	//Overtaking
	// * You may accelerate even if cruise control is active, in order to overtake other vehicles.  Let go of the
	//    accelerator to let the cruise control coast back to normal speed.

	//Increase Speed
	// * Tap the Resume button once while the cruise control system is holding speed to increase set speed by 1 mph (default).
	// * Press and hold the Resume button to accelerate

	//Coast
	// * Press and hold the Coast button to deactivate the cruise control.
	// * Tap the Coast button

	//Automatic Braking (UNIMPLEMENTED)
	// * (Select Models Only) If your vehicle includes automatic braking, it will decelerate to hold speed if the
	//    vehicle begins to accelerate downhill.
	// * Cruise control will shut off automatically upon collisions, if your vehicle falls below the minimum speed, or if you tap the brakes or engage the handbrake.
	// * Bicycles are disabled by default.  Highly unrealistic.
	// * Aircraft are not included.  They are included in the Airframe module instead.  This is because a "cruise control" for aircraft is entirely unsuitable:
	//   * No altitude hold.  No attitude hold.  No auto-level.  No heading.
	//   * There is a high danger of stalling or "controlled flight into terrain".

	///<summary>Global enable state for the cruise control module.</summary>
	public static bool Enabled {
		get { return cruise_control_enabled != Core.EnableState.Off; }
		set {
			if(value && cruise_control_enabled != Core.EnableState.Off) {
				cruise_control_enabled = Core.EnableState.On;
				//Tick += RealismCruiseTick;
				//if(cruise_control_key != Keys.None) KeyDown += RealismCruiseKeyDown;
				}
			else {
				cruise_control_enabled = Core.EnableState.Off;
				//Tick -= RealismCruiseTick;
				//KeyDown -= RealismCruiseKeyDown;
				}
			}
		}
	///<summary>Models for which cruise control is enabled.  None (turn off module), Select whitelisted models, all vehicles except in Blacklist, or All vehicles.</summary>
	///<remarks>Any value not equal to zero indicates module is on.</remarks>
	public static Core.EnableState EnableState {
		get { return cruise_control_enabled; }
		set { cruise_control_enabled = value; }
		}
	
	///<summary>Is the cruise control system on and ready to activate?  Use <see cref="IsActive"/> instead if you want to
	/// check actual operational status.</summary>
	public static bool IsOn {
		get { return cruise_control_on; }
		}
	///<summary>Is the cruise control system currently engaged and holding speed?</summary>
	public static bool IsActive {
		get { return cruise_control_on && !_cancelled; }
		}
	
	///<summary>The current (desired) held speed of the cruise control system, applicable only if
	/// <see cref="IsActive"/> is true.</summary>
	public static float CruiseControlHoldSpeed {
		get { return cruise_hold_speed; }
		set { cruise_hold_speed = value; }
		}
	
	///<summary>Whether cruise control is also available for bicycles.  Player convenience feature.</summary>
	///<remarks>Even if EnableState is for Select models only, bicycles will still be enabled.</remarks>
	public static bool AllowBicycles {
		get { return cruise_control_bicycles; }
		set { cruise_control_bicycles = value; }
		}
	
	///<summary>Should vehicles which are airborne with the cruise control system rev like crazy until they land?</summary>
	///<remarks>Not very realistic (cruise control is realistically based on wheel speed), but a lot of fun.</remarks>
	public static bool OverRevInAir {
		get { return overrev_in_air; }
		set { overrev_in_air = value; }
		}
	
	///<summary>Adds <see cref="Vehicle"/> <param name="veh">veh</param> to the cruise control model whitelist.</summary>
	///<returns>True if added. False if already present in list.</returns>
	public static bool WhitelistVehicle(Vehicle veh) {
		return WhitelistVehicle((VehicleHash)veh.Model.Hash);
		}
	///<summary>Adds <see cref="VehicleHash"/> <param name="hash">hash</param> to the cruise control model whitelist.</summary>
	///<returns>True if added. False if already present in list.</returns>
	public static bool WhitelistVehicle(VehicleHash hash) {
		if(cruise_control_models.Contains(hash)) return false;
		cruise_control_models.Add(hash);
		return true;
		}
	///<summary>Adds <see cref="Vehicle"/> <param name="veh">veh</param> to the cruise control model blacklist.</summary>
	///<returns>True if added. False if already present in list.</returns>
	public static bool BlacklistVehicle(Vehicle veh) {
		return BlacklistVehicle((VehicleHash)veh.Model.Hash);
		}
	///<summary>Adds <see cref="VehicleHash"/> <param name="hash">hash</param> to the cruise control model blacklist.</summary>
	///<returns>True if added. False if already present in list.</returns>
	public static bool BlacklistVehicle(VehicleHash hash) {
		if(cruise_control_blacklist.Contains(hash)) return false;
		cruise_control_blacklist.Add(hash);
		return true;
		}
	///<summary>Removes <see cref="Vehicle"/> <param name="veh">veh</param> to the cruise control model whitelist.</summary>
	///<returns>True if removed. False if not present in list.</returns>
	public static bool RemoveWhitelistVehicle(Vehicle veh) {
		return RemoveWhitelistVehicle((VehicleHash)veh.Model.Hash);
		}
	///<summary>Removes <see cref="VehicleHash"/> <param name="hash">hash</param> to the cruise control model blacklist.</summary>
	///<returns>True if removed. False if not present in list.</returns>
	public static bool RemoveWhitelistVehicle(VehicleHash hash) {
		if(!cruise_control_models.Contains(hash)) return false;
		cruise_control_models.RemoveAll(X => (X == hash));
		return true;
		}
	///<summary>Removes <see cref="Vehicle"/> <param name="veh">veh</param> to the cruise control model blacklist.</summary>
	///<returns>True if removed. False if not present in list.</returns>
	public static bool RemoveBlacklistVehicle(Vehicle veh) {
		return RemoveBlacklistVehicle((VehicleHash)veh.Model.Hash);
		}
	///<summary>Removes <see cref="VehicleHash"/> <param name="hash">hash</param> to the cruise control model blacklist.</summary>
	///<returns>True if removed. False if not present in list.</returns>
	public static bool RemoveBlacklistVehicle(VehicleHash hash) {
		if(!cruise_control_blacklist.Contains(hash)) return false;
		cruise_control_blacklist.RemoveAll(X => (X == hash));
		return true;
		}
	
	///<summary>List of modelhashes for vehicles that have cruise control systems.</summary>
	public static List<VehicleHash> CruiseControlModels {
		get { return cruise_control_models; }
		set { cruise_control_models = value; }
		}
	///<summary>List of modelhashes for vehicles that are specifically excluded from cruise control.</summary>
	public static List<VehicleHash> CruiseControlBlacklist {
		get { return cruise_control_blacklist; }
		set { cruise_control_blacklist = value; }
		}
	
	///<summary><see cref="Keys"/> key to press to activate/deactivate cruise control, or to reset speed
	/// if your set speed has changed. Default C.</summary>
	public static Keys CruiseControlKey {
		get { return cruise_control_key; }
		set { cruise_control_key = value; }
		}
	///<summary>Keyboard metakeys to press to activate/deactivate cruise control (0 = None, 1 = Alt, 2 = Ctrl, 4 = Shift).
	/// Combinations are valid (i.e., 3 = Alt+Ctrl, 5 = Alt+Shift, 6 = Ctrl+Shift, 7 = Alt+Ctrl+Shift). Default
	/// None.</summary>
	public static Core.KeyboardMeta CruiseControlMetakeys {
		get { return cruise_control_metakey; }
		set { cruise_control_metakey = value; }
		}
	
	///<summary><see cref="Core.GamePadButton"/> to press to activate cruise control, or to reset speed if your set
	/// speed has changed. Default (A)=1.</summary>
	public static Core.GamePadButton CruiseControlButton {
		get { return cruise_control_button; }
		set { cruise_control_button_modifier = value; }
		}
	///<summary><see cref="Core.GamePadButton"/> that must be held in order ot use <see cref="CruiseControlButton"/>.
	/// Default (DPadRight)=32.</summary>
	public static Core.GamePadButton CruiseControlButtonModifier {
		get { return cruise_control_button; }
		set { cruise_control_button_modifier = value; }
		}
	
	///<summary>Key to increase set speed per tap, press and hold to accelerate desired set speed, or resume cruise
	/// control at last set speed if system was cancelled. Unassigned by default.</summary>
	public static Keys AccelKey {
		get { return cruise_control_up; }
		set { cruise_control_up = value; }
		}
	///<summary>Metakey combination necessary to use <see cref="AccelKey"/> (0 = None,
	/// 1 = Alt, 2 = Ctrl, 4 = Shift). Combinations are valid.</summary>
	public static Core.KeyboardMeta AccelMetakeys {
		get { return cruise_control_up_metakey; }
		set { cruise_control_up_metakey = value; }
		}
	
	///<summary>Key to decrease set speed per tap, or press and hold to coast to desired speed, or to set current speed
	/// and reactivate system if currently cancelled.  Unassigned by default.</summary>
	public static Keys DecelKey {
		get { return cruise_control_down; }
		set { cruise_control_down = value; }
		}
	///<summary>Metakey combination necessary to use <see cref="DecelKey"/> (0 = None, 1 = Alt, 2 = Ctrl,
	/// 4 = Shift). Combinations are valid.</summary>
	public static Core.KeyboardMeta DecelMetakeys {
		get { return cruise_control_down_metakey; }
		set { cruise_control_down_metakey = value; }
		}
	
	///<summary>Key to cancel cruise control system without clearing set speed. Unassigned by default.</summary>
	public static Keys CancelKey {
		get { return cruise_control_cancel; }
		set { cruise_control_cancel = value; }
		}
	///<summary>Metakey combination necessary to use <see cref="CancelKey"/> (0 = None, 1 = Alt, 2 = Ctrl,
	/// 4 = Shift). Combinations are valid.</summary>
	public static Core.KeyboardMeta CancelMetakeys {
		get { return cruise_control_cancel_metakey; }
		set { cruise_control_cancel_metakey = value; }
		}
	
	internal static Core.EnableState cruise_control_enabled = Core.EnableState.Blacklist;

	internal static List<VehicleHash> cruise_control_models = new List<VehicleHash>() { };
	internal static List<VehicleHash> cruise_control_blacklist = new List<VehicleHash>() { };

	internal static bool cruise_control_bicycles = false;
	
	internal static Keys cruise_control_key = Keys.C;
	internal static Core.KeyboardMeta cruise_control_metakey = Core.KeyboardMeta.None;

	internal static Keys cruise_control_up = Keys.None;
	internal static Core.KeyboardMeta cruise_control_up_metakey = Core.KeyboardMeta.Shift;
	
	internal static Keys cruise_control_down = Keys.None;
	internal static Core.KeyboardMeta cruise_control_down_metakey = Core.KeyboardMeta.Ctrl;
	
	internal static Keys cruise_control_cancel = Keys.None;
	internal static Core.KeyboardMeta cruise_control_cancel_metakey = Core.KeyboardMeta.None;

	///Gamepad button to press to activate/deactivate cruise control.
	/// 0 = None
	/// 1 = A
	/// 2 = B
	/// 3 = X
	/// 4 = Y
	/// 5 = LB
	/// 6 = RB
	/// 7 = LT
	/// 8 = RT
	/// 9 = LS
	/// 10 = RS
	/// 11 = Start
	/// 16 = DPadUp
	/// 32 = DPadRight
	/// 64 = DPadDown
	/// 128 = DPadLeft
	///Can also use a DPad mask by adding two DPad directions (e.g., 48 = Up+Right)
	///Note: Any button bound to your Handbrake or Brake will not work for cruise control, because cruise control
	/// will shut off while either of these controls are pushed.
	internal static Core.GamePadButton cruise_control_button = Core.GamePadButton.A;
	
	internal static Core.GamePadButton cruise_control_button_modifier = Core.GamePadButton.DPadRight;

	///<summary>Should the cruise control system press the "Set" button immediately after activation?</summary>
	internal static bool auto_activate_cruise = true;

	///<summary>Minimum speed below which cruise control cannot be activated.</summary>
	///<remarks>Set to a negative value to disable.</remarks>
	internal static float cruise_control_minimum_speed = 5.0f; // m/s
	///<summary>Minimum speed of cruise control for bicycles, if enabled.</summary>
	internal static float cruise_bicycle_minimum_speed = 1.0f; // m/s
	
	//Automatic braking cruise control (player only, selected models only)
	// * Automatic operation (cannot be shut off except by configuration)
	// * If desired speed is < 2 m/s lower than current speed, do not apply brake pressure
	// * Else, apply 1% brake pressure
	// * Each frame, increase brake pressure by estimated value required to achieve 1.0 m/s deceleration
	// * Each frame, decrease brake pressure by estimated value once desired speed is < 2 m/s lower than current speed
	// * If deceleration occurs even at 1% brake pressure, disengage.
	internal static List<VehicleHash> automatic_braking_models = new List<VehicleHash>() {
		VehicleHash.Dilettante, VehicleHash.Dilettante2
		};

	///<summary>Can vehicles brake automatically when exceeding set speed under cruise control (selected models: modern cars, luxury cars, and electric vehicles)?</summary>
	internal static Core.EnableState cruise_braking_enabled = Core.EnableState.Select;

	///<summary>Threshold above set speed where automatic braking engages or disengages.</summary>
	internal static float cruise_braking_threshold = 1.5f; // m/s

	//Adaptive cruise control (player only, selected models only) (UNIMPLEMENTED)
	// * Automatic operation (cannot be shut off except by configuration)
	// * Adaptive cruise implies automatic braking (all adaptive cruise models have automatic braking).
	// * Scan for vehicles in front and, if finding one in the driving path (based on a 35-degree steering radius and current amount of steering input):
	//   * If our speed is <1.0 m/s faster and they are > 31 m away, desired speed is their speed + 1.0 m/s or hold_speed, whichever is less
	//   * If our speed is <0.5 m/s faster and they are > 29 m away, desired speed is their speed
	//   * If our speed is >=1.0 m/s faster and they are < 29 m away, desired speed is their speed - 1.0 m/s or hold speed, whichever is less
	//   * If our speed is >=10 m/s faster and they are < 29 m away, engage emergency braking
	//   * If target vehicle speed is under 5 m/s, set follow distance to equal target vehicle's speed + 1 m (e.g., at 2 m/s, follow at 3 m distance).
	// * Models with adaptive cruise have no minimum threshold for the cruise control system to deactivate automatically, but they still cannot
	//    activate if the vehicle's speed is below the minimum.
	// * TODO refinements:
	//   * read steering radius from handling data
	internal static List<VehicleHash> adaptive_cruise_models = new List<VehicleHash>();
	
	internal static Core.EnableState adaptive_cruise_enabled = Core.EnableState.Select;
	
	///Range at which adaptive cruise vehicles will scan for obstacles.
	internal static float adaptive_scan_range = 100f; // metres

	internal static float default_output = 0.40f;

	///<summary>Whether the cruise control system is currently running on the player's vehicle.</summary>
	internal static bool cruise_control_on = false;
	///<summary>Whether the cruise control system will shut down on the next frame (due to collision, etc.).</summary>
	private static bool _should_deactivate = false;
	///<summary>Whether the cruise control system will retain set speed but cancel on the next frame (due to braking, etc.)</summary>
	private static bool _should_cancel = false;

	///<summary>If true, the cruise control system is running, but not maintaining the held speed -- i.e., it has been cancelled.</summary>
	private static bool _cancelled = false;

	internal static float cruise_hold_speed = 0.0f; // m/s
	internal static bool overrev_in_air = true;

	///<summary>The floating percent output of the player's vehicle from 0.0 to 1.0.  Will be adjusted up if the
	/// vehicle is still decelerating even when the cruise control is working, and will be reduced back to normal
	/// when the cruise control reaches the set speed again.</summary>
	private float _cruise_control_current_output = 0.40f; // percent (0.0 to 1.0)
	private float _last_speed = 0.0f; // m/s
	//private float _accumulated_error = 0.0f;

	private bool _brake_enabled_veh = false;
	private int _last_gear = 0;

	private static GTA.Math.Vector3 _last_position = new GTA.Math.Vector3(0, 0, 0);

	private void RealismCruiseKeyDown(object sender, KeyEventArgs parameters) {
		if(parameters.KeyCode == cruise_control_up) {
			}
		if(parameters.KeyCode == cruise_control_key && Core.IsPlayerDriving) {
			Vehicle pv = Game.Player.Character.CurrentVehicle;
			VehicleData vdata = Core.VehicleData(pv);
			if(HasCruiseControl(pv, vdata)) {
				if(Core.MatchModifiers(cruise_control_metakey, parameters)) {
					if(cruise_control_on == false && pv.Speed >= cruise_control_minimum_speed) {
						cruise_control_on = true;
						if(auto_activate_cruise) {
							UI.Notify("~g~Cruise control activated.~s~");
							cruise_hold_speed = Game.Player.Character.CurrentVehicle.Speed;
							LinkToTransmissionMod(pv, vdata);
							}
						}
					else if(cruise_control_on) {
						if(_cancelled && auto_activate_cruise) {
							_cancelled = false;
							UI.Notify("~g~Cruise control resumed.~s~");
							cruise_hold_speed = Game.Player.Character.CurrentVehicle.Speed;
							}
						else if(!_cancelled && Math.Abs(Game.Player.Character.CurrentVehicle.Speed - cruise_hold_speed) > 1.0f) {
							cruise_hold_speed = Game.Player.Character.CurrentVehicle.Speed;
							}
						else {
							_should_deactivate = true;
							}
						}
					}
				}
			else {
				cruise_control_on = false;
				}
			}
		}
		
	private void CruiseControlTick(object sender, EventArgs parameters) {
		if(!Core.IsPlayerDriving) {
			if(cruise_control_on) {
				Interval = 600;
				//Clear any "locked" accelerations
				Core.DebugNotify("Exiting vehicle with active cruise, clearing locked controls this frame.");
				Game.SetControlNormal(0, GTA.Control.VehicleAccelerate, 0.0f);
				Game.SetControlNormal(0, GTA.Control.VehicleBrake, 0.0f);
				cruise_control_on = false;
				}
			return;
			}
	
		//Remove event handler if cruise control system is globally disabled (impossible in base mod, but if an
		// in-game configuration menu is ever added this will handle it)
		if(cruise_control_enabled == Core.EnableState.Off) {
			Tick -= CruiseControlTick;
			KeyDown -= RealismCruiseKeyDown;
			return;
			}
		Interval = 1;

		Vehicle veh = Game.Player.Character.CurrentVehicle;
		VehicleData vdata = Core.VehicleData(veh);
		if(!HasCruiseControl(veh, vdata)) return;
		
		_brake_enabled_veh = vdata.HasCruiseAutomaticBraking;
		
		//TODO: re-add keydown handler if cruise control feature is disabled then subsequently re-enabled at runtime
		// Not necessary in default externally-configured build.

		if(!cruise_control_on) return;
		
		//Proceed if vehicle hash is whitelisted
		if(cruise_control_enabled == Core.EnableState.Select && (!cruise_control_models.Contains((VehicleHash)veh.Model.Hash))) return;
		//Proceed if vehicle hash is not blacklisted
		else if(cruise_control_enabled == Core.EnableState.Blacklist && cruise_control_models.Contains((VehicleHash)veh.Model.Hash)) return;
		//If this point is reached, the car is either whitelisted or not blacklisted, or the enable state is always-on

		if(Game.Player.Character.LastVehicle.HasCollidedWithAnything) {
			_should_deactivate = true;
			Core.DebugNotify("Cruise control deactivated by collision sensor.");
			}
		else {
			if(Core.ControlHeld(GTA.Control.VehicleBrake) && !_cancelled) {
				_should_cancel = true;
				Core.DebugNotify("Cruise control cancelled by brake.");
				}
			else if(Core.ControlHeld(GTA.Control.VehicleHandbrake) && !_cancelled) {
				_should_cancel = true;
				Core.DebugNotify("Cruise control cancelled by handbrake.");
				}
			if(Game.Player.Character.LastVehicle.Speed < cruise_control_minimum_speed && !_cancelled) {
				_should_deactivate = true;
				Core.DebugNotify("Cruise control below minimum threshold for activation.");
				}
			if(Transmission.ManualTransmissionModPresent && !_cancelled) {
				if(Core.GetIntDecorator(veh, Transmission.MT_GEAR) != _last_gear && Core.GetIntDecorator(veh, Transmission.MT_GET_SHIFT_MODE) != (int)Core.ShifterMode.AUTOMATIC) {
					_should_cancel = true;
					Core.DebugNotify("Cruise control cancelled due to operator input on shifter.");
					}
				}
			}
		
		if(Transmission.ManualTransmissionModPresent) {
			_last_gear = Core.GetIntDecorator(veh, Transmission.MT_GEAR);
			}
		
		if(_should_deactivate) {
			_cruise_control_current_output = default_output;
			_last_speed = 0.0f;
			_should_cancel = _cancelled = false;
			UI.Notify("~g~Cruise control deactivated.~s~");

			cruise_control_on = _should_deactivate = _cancelled = false;
			return;
			}
		if(_should_cancel) {
			_cancelled = true;
			_should_cancel = false;
			UI.Notify("~g~Cruise control cancelled.~s~");
			return;
			}
		if(_cancelled) {
			return;
			}
		
		//Cruise control becomes confused when airborne
		// More realistically it would cut throttle, since wheel speed (which is what actually determines the
		// displayed speed on a speedometer) will rev up almost effortlessly; but this is more entertaining.
		if(veh.IsInAir && overrev_in_air) {
			Game.SetControlNormal(0, GTA.Control.VehicleAccelerate, 1.0f);
			return;
			}
		
		float FPS = Math.Max(Game.FPS, 1);
		
		float desired_distance = veh.Speed / FPS;
		if(_last_position.X == 0 && _last_position.Y == 0 && _last_position.Z == 0) _last_position = veh.Position;
		float actual_distance = _last_position.DistanceTo(veh.Position);

		float corrected_speed = (float)Math.Pow(cruise_hold_speed, 1.01f);
		//float adaptive_cruise_speed = 0.0f;
		//float target_speed = Math.Min(corrected_speed, adaptive_cruise_speed);
		float target_speed = corrected_speed;

		float desired_accel = veh.Speed < cruise_hold_speed ? Math.Min(3.5f, (1 + corrected_speed - veh.Speed))/FPS : 0f; //magic numbers for the moment, will be externalised
		float actual_accel = Acceleration();

		float speed_error = (cruise_hold_speed - veh.Speed) * 1f;
		float distance_error = (desired_distance - actual_distance) * 50f;
		float accel_error = (desired_accel - actual_accel) * 2f;
		_last_position = veh.Position;

		_cruise_control_current_output = Math.Max(0.0f, Math.Min(speed_error + distance_error + accel_error, 1.0f));
		Game.SetControlNormal(0, GTA.Control.VehicleAccelerate, _cruise_control_current_output);

		//Automatic braking
		if(cruise_braking_enabled >= Core.EnableState.Select && _brake_enabled_veh && _cruise_control_current_output <= 0 && cruise_hold_speed < veh.Speed) {
			if(veh.Speed > cruise_hold_speed + cruise_braking_threshold) {
				float braking_output = Math.Max(0.0f, -1f * Math.Min(speed_error + distance_error + accel_error, 1.0f));
				Game.SetControlNormal(0, GTA.Control.VehicleBrake, braking_output);
				}
			}
		
		/*
		UI.ShowSubtitle("SPDerr: " + speed_error +
			", DSTerr: " + distance_error +
			", ACCerr: " + accel_error +
			", OUT: " + _cruise_control_current_output +
			", SPD: " + veh.Speed +
			", SET: " + cruise_hold_speed);
		*/
		}
	
	private void LinkToTransmissionMod(Vehicle veh, VehicleData vdata) {
		if(!Transmission.ManualTransmissionModPresent) return;
		_last_gear = Core.GetIntDecorator(veh, Transmission.MT_GEAR);
		}
	
	private bool HasCruiseControl(Vehicle veh, VehicleData vdata) {
		if(!cruise_control_bicycles && veh.Model.IsBicycle) return false;
		if((veh.Model.IsHelicopter || veh.Model.IsPlane)) return false;
		if(cruise_control_enabled == Core.EnableState.Select) return (cruise_control_models.Contains((VehicleHash)veh.Model.Hash) || vdata.HasCruiseControl);
		if(cruise_control_enabled == Core.EnableState.Blacklist && cruise_control_blacklist.Contains((VehicleHash)veh.Model.Hash)) return false;
		return true;
		}
		
	private float Acceleration() {
		Vehicle veh = Game.Player.Character.CurrentVehicle;
		float rate = (veh.Speed - _last_speed);
		_last_speed = veh.Speed;
		return rate;
		}
}

}
