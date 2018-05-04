using GTA;
using GTA.Math;
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using XInputDotNetPure;
using System.Linq;

namespace RealismVehicles
{

public class Indicators : Script
{
	public Indicators() {
		Interval = 80; //no need for greater than 12 fps with this system
			
		if(Enabled) {
			if((indicator_left_key != Keys.None && indicator_right_key != Keys.None) ||
				(indicator_left_button != Core.GamePadButton.None ||
					indicator_right_button != Core.GamePadButton.None) ||
				(hazard_key != Keys.None))
				{
				KeyDown += RealismIndicatorsKeyDown;

				if((enable_indicator_yield) ||
						(enable_hazard_yield &&
							(hazard_yield_mode_on != IndicatorsYieldMode.NO_YIELD ||
							hazard_yield_mode_off != IndicatorsYieldMode.NO_YIELD)
							)
						)
					{
					Tick += RealismIndicatorsTick;
					}
				}
			}
		}
	
	///// INDICATORS /////
	//Turn Signal Controls
	// * Use your turn signals just like you would use a real turn signal lever.
	// * Press Indicator controls (default Left and Right) to activate corresponding indicator.
	// * To cancel signals manually, press in the opposite direction to cancel indicator.
	// * With optional "single tap" mode, press SAME indicator to cancel and press OPPOSITE indicator to
	//    activate, which reduces keypresses needed when switching signals.  This is not the default because it is
	//    less intuitive.

	//Hazard Lights
	// * Press Hazard control (default Down, if cell phone not open) to activate hazard lights.
	// * Press Hazard control again to switch back off.

	//Signal Cancellation (UNIMPLEMENTED)
	// * Signal controls will cancel automatically, using one (or both) of two modes:
	//   * If using the distance mode, once your car turns at least 30 degrees, your signals will cancel after
	//      travelling at least 5 metres without turning more than 10 degrees.  This is intended for mouse and
	//      keyboard users.
	//   * If using the steering mode, once your steering control turns at least 15%, your signals will cancel after
	//      returning within 2.5% of the centre.  This is intended for gamepad/steering wheel users.
	
	//Yielding (UNIMPLEMENTED)
	// * If your turn signal is on, vehicles travelling in parallel on that flank will yield to you (slow to your
	//    speed minus 1.0 m/s, to an obvious minimum of zero, unless their speed is at least 5 m/s faster than
	//    yours).
	// * If your hazard lights are OFF, vehicles behind you will wait while you are stopped.  Vehicles travelling
	//    perpendicular to you will pass.  This behaviour can be changed as desired.
	// * If your hazard lights are ON, vehicles behind you will pass while you are stopped.  Vehicles travelling
	//    perpendicular to you will wait.  This behaviour can be changed as desired.
	// * The state of your turn signals will be ignored while your hazard lights are on (other mods often forget
	//    to do this), but will resume when your hazard lights are switched off.
	// * Vehicles that are permitted to pass will use vanilla AI behaviour to pass you, so please park safely.
	// * A timer will be established while your hazard lights are off.  If this timer expires without your hazard
	//    lights being switched on, permission will be granted to pass you again.  However, this timer is set
	//    arbitrarily high (180 seconds by default).
	// * Switching off your hazard lights will reset the timer, forcing new vehicles to yield.
	// * In a civilian car, vehicles that had already been granted right of way will continue to retain right of
	//    way regardless of your hazards.
	// * In a commercial vehicle or utility vehicle, vehicles that had already been granted right of way will
	//    yield right of way upon setting your hazards.
	// * A utility vehicle or large truck with hazard lights on will halt traffic in a 15 metre radius as long
	//    as they are in motion, with up to a five second allowance while halted, allowing them to complete
	//    manoeuvres if necessary.
	
	public enum IndicatorsYieldMode
		{
		///<summary>No one will yield to your vehicle.</summary>
		NO_YIELD,
		///<summary>Traffic on cross streets or otherwise perpendicular to your vehicle will yield.</summary>
		PERPENDICULAR_YIELD,
		///<summary>Traffic on the same street or otherwise parallel to your vehicle will yield.</summary>
		LONGITUDINAL_YIELD,
		///<summary>All vehicles in range of your vehicle will yield.</summary>
		ALL_YIELD = PERPENDICULAR_YIELD | LONGITUDINAL_YIELD
		}
	
	public enum SignalCancellationMode
		{
		///<summary>Turn signals will never cancel automatically.</summary>
		DISABLED,
		///<summary>Turn signals will cancel automatically after turning at least 30 degrees and then travelling
		/// at least 5 metres with minimal change in direction.</summary>
		DISTANCE,
		///<summary>Turn signals will cancel after the turning control is turned past at least 15%, and then
		/// returned to less than 2.5%.</summary>
		CONTROLLER 
		}

	public enum Indicator
		{
		LEFT,
		RIGHT
		}
	
	public enum HeadlightsMode
		{
		///<summary>Allow the game to control the vehicle's headlights.</summary>
		AUTO = -1,
		///<summary>Force the vehicle's headlights to remain off.</summary>
		OFF = 0,
		///<summary>Turn on running lights but "destroy" headlights.</summary>
		RUNNING_LIGHTS,
		///<summary>Turn on low beam headlights.</summary>
		LOW_BEAM,
		///<summary>Turn on high beam headlights.</summary>
		HIGH_BEAM,
		///<summary>Turn on running lights but "destroy" headlights.</summary>
		PARKING_LIGHTS = RUNNING_LIGHTS,
		///<summary>Turn on low beam headlights.</summary>
		DIPPED_BEAM = LOW_BEAM,
		///<summary>Turn on high beam headlights.</summary>
		FULL_BEAM = HIGH_BEAM
		}
	
	//Turn Signal Controls
	
	///<summary>Returns true if the player's current vehicle has any turn signal on (i.e., will other drivers know that this
	/// vehicle intends to change lanes?).</summary>
	///<remarks>If the vehicle has its hazard lights on, it does not have a turn signal "on" even though the indicator lamps
	/// are active.</remarks>
	public bool IsTurnSignalOn {
		get {
			if(!Core.IsPlayerDriving) return false;
			return !_hazards_on && (_indicator_left_on || _indicator_right_on);
			}
		}
	///<summary>True if the player is signalling to go left.  False if the hazard lights are also on.</summary>
	public bool IsLeftSignalOn {
		get {
			if(!Core.IsPlayerDriving) return false;
			return !_hazards_on && _indicator_left_on;
			}
		}
	///<summary>True if the player is signalling to go right. False if the hazard lights are also on.</summary>
	public bool IsRightSignalOn {
		get {
			if(!Core.IsPlayerDriving) return false;
			return !_hazards_on && _indicator_right_on;
			}
		}
	
	///<summary>Whether the left turn signal lamp is currently lit. This concerns itself with whether the
	/// indicator lamps are mechanically on; if the vehicle has its hazard lights on, both the left and right
	/// indicators ARE on.</summary>
	public bool LeftIndicatorOn { get { return _indicator_left_on; } }
	///<summary>Whether the right turn signal lamp is currently lit. This concerns itself with whether the
	/// indicator lamps are mechanically on; if the vehicle has its hazard lights on, both the left and right
	/// indicators ARE on.</summary>
	public bool RightIndicatorOn { get { return _indicator_right_on; } }
	
	//If either key is disabled, the game will automatically ignore (both) turn signal key inputs from this mod for
	// efficiency, but gamepad and yielding can still be enabled/disabled separately.

	///<summary>Assigns or reads key which operates the left turn signal.</summary>
	public Keys LeftSignalKey {
		get { return indicator_left_key; }
		set { indicator_left_key = value; }
		}
	///<summary>Assigns or reads key which operates the right turn signal.</summary>
	public Keys RightSignalKey {
		get { return indicator_right_key; }
		set { indicator_right_key = value; }
		}
	///<summary>Assigns or reads keyboard modifiers (Alt, Ctrl, Shift) required to activate the left indicator.</summary>
	///<remarks>Alt = 1, Ctrl = 2, Shift = 4.  Any sum of the corresponding digits is valid (e.g., 6 = Ctrl+Shift).</remarks>
	public Core.KeyboardMeta LeftSignalMetakey {
		get { return indicator_left_metakey; }
		set { indicator_left_metakey = value; }
		}
	///<summary>Assigns or reads keyboard modifiers (Alt, Ctrl, Shift) required to activate the right indicator.</summary>
	///<remarks>Alt = 1, Ctrl = 2, Shift = 4.  Any sum of the corresponding digits is valid (e.g., 6 = Ctrl+Shift).</remarks>
	public Core.KeyboardMeta RightSignalMetakey {
		get { return indicator_right_metakey; }
		set { indicator_right_metakey = value; }
		}

	///If either button is disabled, the game will automatically ignore the gamepad for this feature, but will still support
	/// the keyboard and yielding if those options are enabled separately.
	public Core.GamePadButton LeftSignalButton {
		get { return indicator_left_button; }
		set { indicator_left_button = value; }
		}
	public Core.GamePadButton RightSignalButton {
		get { return indicator_right_button; }
		set { indicator_right_button = value; }
		}
	///These are the "shift" buttons, which must be held on the gamepad before the primary button
	/// will register.  For example, if DPadDown is the shift button and LB is the primary button,
	/// you would hold DPadDown and tap RB to activate.
	public Core.GamePadButton LeftSignalShiftButton {
		get { return indicator_left_button_modifier; }
		set { indicator_left_button_modifier = value; }
		}
	public Core.GamePadButton RightSignalShiftButton {
		get { return indicator_right_button_modifier; }
		set { indicator_right_button_modifier = value; }
		}
	
	//--------------------

	//Hazard Controls

	///<summary>Returns true if the player's current vehicle has its hazard lights on.</summary>
	public bool HazardsOn {
		get {
			if(!Game.Player.Character.IsInVehicle()) return false;
			return _hazards_on;
			}
		}

	///<summary>If false, the turn signal and yielding feature is entirely disabled.</summary>
	public bool Enabled {
		get { return indicator_controls_enabled; }
		}

	///<summary>Whether tapping the opposite signal will activate that opposite signal or simply deactivate the
	/// current signal.</summary>
	///<remarks>If false (default), you may cancel a signal by tapping the opposite signal.  You will therefore have
	/// to tap the signal twice to switch on the opposite signal if you change your mind.
	///<para>If true, a signal is cancelled by tapping the same signal, and tapping the opposite signal will switch to
	/// that signal instead.  This is faster and easier for quick direction changes if you prefer to rely on automatic
	/// cancellation, but much less intuitive for manual control.</para></remarks>
	public bool SingleTapMode {
		get { return indicator_mode_single_tap; }
		set { indicator_mode_single_tap = value; }
		}

	///True if the player is using the distance-based automatic turn signal cancel (intended for keyboard users).
	public bool DistanceCancelEnabled {
		get {
			return (CancelDistance > 0.0f) && (CancelAngle > 0.0f);
			}
		}
	///True if the player is using the steering-based automatic turn signal cancel (intended for gamepad and
	/// steering wheel users).
	public bool SteeringCancelEnabled {
		get {
			return (CancelSteeringThreshold > 0.0f) &&
				(CancelSteeringThreshold <= 100.0f) &&
				(CancelSteeringReset >= -100.0f) &&
				(CancelSteeringReset <= 100.0f);
			}
		}

	//--------------------

	///<summary>Distance you need to travel after changing heading before your turn signal will cancel.</summary>
	///<remarks>Setting to value equal to or lower than zero will disable this feature.  Intended for keyboard users.
	/// Value is expressed in metres.</remarks>
	public float CancelDistance {
		get { return indicator_cancel_distance; }
		set { indicator_cancel_distance = value; }
		}
		
	///<summary>Heading change required before your turn signal will cancel.</summary>
	///<remarks>Setting to value equal to or lower than zero will disable this feature.  Intended for keyboard users.
	/// Value is expressed in metres.</remarks>
	public float CancelAngle {
		get { return indicator_cancel_heading_change; }
		set { indicator_cancel_heading_change = value; }
		}

	///<summary>Percentage (0.0 to 100.0) of steering range where your signal will be told to cancel the next time you
	/// straighten your wheels.</summary>
	///<remarks><para>''i.e.'', once you adjust the steering wheel past this point, the turn signals are
	/// "condemned" and will cancel as soon as your wheels straighten again.  Intended for gamepad or steering
	/// wheel users.</para>
	///<para>Disable by setting to a negative value (less than 0.0), or an impossibly high positive value (above 100.0).</para>
	///<para>Keyboard users may want to disable this feature, as the steering threshold will be passed instantly at any
	/// level of keyboard steering input.</para>
	///<para>Gamepad users may want to increase the percentage required somewhere between 30~50 %, as an analogue stick
	/// can be moved quickly enough to make accidental signal cancellation likely.</para>
	///<para>For steering wheel users, the amount of wheel turn necessary to pass this threshold depends on your
	/// "steering lock".  For instance, if using a Logitech G27 steering wheel with the full range set to 900.0
	/// degrees, the default of 15.0% requires you to turn the wheel at least 67.5 degrees before the signal can
	/// cancel.  If your steering lock is lower, either in the configuration of your steering wheel or with a "soft
	/// lock" added by a mod like ikt's Manual Transmission mod (which uses a default of 720.0 degrees), the angle
	/// you need to turn your wheel will be correspondingly less.</para>
	///<para>Formula: angle of turn = (steering lock / 2) * threshold</para>
	///</remarks>
	public float CancelSteeringThreshold {
		get { return indicator_cancel_steering_threshold; }
		set { indicator_cancel_steering_threshold = value; }
		}
	///<summary>Percentage (0.0 to 100.0) of steering range where your signal will actually cancel once you return your
	/// steering wheel back towards neutral, using the steering threshold method.</summary>
	///<remarks>Slightly positive values close to zero are recommended.</remarks>
	public float CancelSteeringReset {
		get { return indicator_cancel_reset_threshold; }
		set { indicator_cancel_reset_threshold = value; }
		}

	///<summary>Key to press to activate your vehicle's hazard lights/four-way-flashers.</summary>
	public Keys HazardKey {
		get { return hazard_key; }
		set { hazard_key = value; }
		}
	public Core.KeyboardMeta HazardMetakey {
		get { return hazard_metakey; }
		set { hazard_metakey = value; }
		}

	///<summary>If true, vehicles will match your speed - 1.0 m/s to allow you to merge if your signal is on.</summary>
	///<remarks><para>For this to work, this requires RealismVehicles.net to control your indicators, in case you are
	/// using another mod to control your turn signals, as ScriptHookVDotNet has no accessor method to check if the
	/// signals are currently on.</para>
	///<para>Vehicles travelling faster than 5.0 m/s will yield only if your speed is also at least 5.0 m/s and their
	/// speed is less than 2.5 m/s faster than yours -- in other words, if you merge onto a highway by matching
	/// speed, they will slow to let you in even if travelling faster, but if you want to pull out from the curb,
	/// you'll have to wait -- unless they're an old grandpa driving slow, which will emergently let them be nice
	/// and let you in just like real old grandpas, or if you're trying to pull out into a long chain of cars with
	/// your signal on, the person behind will leave a gap for you.
	/// </para>
	///<para>Vehicles will be checked if they are in the rear quarter flank.  The scan zone is hard-coded for simplicity
	/// and reliability (further outside of 1/4 of your car's width toward the signal side, outside of a 30-degree
	/// offset from your lateral axis, no greater than 5 metres laterally and 20 metres aft, and within scanning
	/// range) and should work properly in almost any real usage scenario.
	/// </para>
	///</remarks>
	public bool EnableSignalYielding {
		get { return enable_indicator_yield; }
		set { enable_indicator_yield = value; }
		}

	///<summary>Whether vehicles will yield to you or pass you depending on facing whenever your hazard lights are
	/// enabled.</summary>
	///<remarks>By default, perpendicular vehicles will pass and parallel vehicles will yield with your hazards off.
	/// Vice versa if on. Specific behaviour is then specified using the HazardMode variables.</remarks>
	public bool EnableHazardYielding {
		get { return enable_indicator_yield; }
		set { enable_indicator_yield = value; }
		}

	public IndicatorsYieldMode WhoYieldsWhenHazardsOff {
		get { return hazard_yield_mode_off; }
		set { hazard_yield_mode_off = value; }
		}
	public IndicatorsYieldMode WhoYieldsWhenHazardsOn {
		get { return hazard_yield_mode_on; }
		set { hazard_yield_mode_on = value; }
		}

	public IndicatorsYieldMode WhoYieldsToServiceWhenHazardsOff {
		get { return hazard_yield_service_off; }
		set { hazard_yield_service_off = value; }
		}
	public IndicatorsYieldMode WhoYieldsToServiceWhenHazardsOn {
		get { return hazard_yield_service_on; }
		set { hazard_yield_service_on = value; }
		}

	public float HazardPerpendicularRange {
		get { return hazard_perpendicular_range; }
		set { hazard_perpendicular_range = value; }
		}
	public float HazardPerpendicularStopDistance {
		get { return hazard_perpendicular_stop; }
		set { hazard_perpendicular_stop = value; }
		}

	public float HazardLongitudinalRange {
		get { return hazard_longitudinal_range; }
		set { hazard_longitudinal_range = value; }
		}
	public float HazardLongitudinalStopDistance {
		get { return hazard_longitudinal_stop; }
		set { hazard_longitudinal_stop = value; }
		}

	public float HazardAllDirectionsRange {
		get { return hazard_all_range; }
		set { hazard_all_range = value; }
		}
	public float HazardAllDirectionsStopDistance {
		get { return hazard_all_stop; }
		set { hazard_all_stop = value; }
		}

	public void Enable() {
		if(!indicator_controls_enabled) {
			KeyDown += RealismIndicatorsKeyDown;
			Tick += RealismIndicatorsTick;
			indicator_controls_enabled = true;
			}
		}
	
	private bool indicator_controls_enabled = true;
	private bool indicator_mode_single_tap = false;
		
	private Keys indicator_left_key = Keys.Left;
	private Core.KeyboardMeta indicator_left_metakey = Core.KeyboardMeta.None;
	private Keys indicator_right_key = Keys.Right;
	private Core.KeyboardMeta indicator_right_metakey = Core.KeyboardMeta.None;

	private Core.GamePadButton indicator_left_button          = Core.GamePadButton.LB;
	private Core.GamePadButton indicator_left_button_modifier = Core.GamePadButton.DPadDown;
	private Core.GamePadButton indicator_right_button          = Core.GamePadButton.RB;
	private Core.GamePadButton indicator_right_button_modifier = Core.GamePadButton.DPadDown;

	private float indicator_cancel_distance = 5.0f; //metres
	private float indicator_cancel_heading_change = 15.0f; //degrees

	private float indicator_cancel_steering_threshold = 15.0f;

	private float indicator_cancel_reset_threshold = 2.5f;

	private Keys hazard_key = Keys.Down;
	private Core.KeyboardMeta hazard_metakey = Core.KeyboardMeta.None;

	private bool enable_indicator_yield = true;
		
	private bool enable_hazard_yield = true;

	///If the default behaviour does not suit you, you can choose which behaviour to apply when the hazard lights
	/// are on and off, separately.  If both are switched to None, the system will automatically disable yielding
	/// simulation for efficiency, including for service vehicles, but will retain the indicator controls unless
	/// those are disabled too (i.e., they will be cosmetic only).
	///0 = No Yield; vehicles obey normal right of way
	///1 = Longitudinal Yield; vehicles behind or in front, if their central position is within one and a half of
	///    your car's width (1/4 width on either side), will yield right of way and match speed with your vehicle
	///    (if you are stopped or travelling towards them, they will stop; if you are travelling slowly in the
	///    same direction as they are, they will also travel slowly).
	///2 = Perpendicular Yield: vehicles within range and within 30 degrees offset of lateral axis will yield
	///    right of way and stop for your vehicle (regardless of your speed)
	///3 = All Yield: all vehicles within range will yield right of way and wait for your vehicle
	private IndicatorsYieldMode hazard_yield_mode_off = IndicatorsYieldMode.LONGITUDINAL_YIELD;
	private IndicatorsYieldMode hazard_yield_mode_on = IndicatorsYieldMode.PERPENDICULAR_YIELD;

	///You can also specify separate behaviour for service vehicles (emergency/commercial/utility vehicles, such
	/// as police cars and heavy trucks).
	private IndicatorsYieldMode hazard_yield_service_off = IndicatorsYieldMode.LONGITUDINAL_YIELD;
	private IndicatorsYieldMode hazard_yield_service_on = IndicatorsYieldMode.ALL_YIELD;
		
	///Maximum range at which vehicles travelling perpendicular to you -- within 90 degrees of your left or right
	/// axis -- will be checked for stopping when your hazard lights are ON.
	private float hazard_perpendicular_range = 20.0f;
		
	///Minimum range by which vehicles will be instructed to stop when travelling perpendicular.  Vehicles will
	/// receive instructions to slow down between the maximum and minimum range, linearly approaching zero at the
	/// stop range.
	private float hazard_perpendicular_stop = 10.0f;

	///Range at which vehicles will be checked for slowing/stopping when your hazard lights are OFF.
	private float hazard_longitudinal_range = 15.0f;

	///Range at which vehicles will be forced to match speed.  Will decelerate linearly to your speed here.
	private float hazard_longitudinal_stop = 2.0f;

	///Range at which vehicles will be instructed to slow down when using an "ALL YIELD" hazard mode.  (By default,
	/// only commercial trucks/service vehicles use this mode.)
	private float hazard_all_range = 30.0f;

	///Range at which vehicles will be forced to come to a halt when using an "ALL YIELD" mode.
	private float hazard_all_stop = 10.0f;

	///Bounding box width as a function of vehicle width.  If another vehicle's centre would enter within a rectangle
	/// equal to width (your car's width * hazard bounding box) and length (hazard parallel range), they will be
	/// instructed to yield or pass depending on your hazard light state.
	private float hazard_bounding_box = 1.5f;

	///The base amount of patience that drivers will have while yielding to a vehicle under the speed limit.
	/// Patience will be randomised +/- 50% of this value.  After their patience expires, they will engage in a
	/// "mild" emotional break (honking, flipping off, or swearing).  If no longer patient, every 1/4 of this time
	/// +/- 50%, they will engage in another emotional break, but in addition to honking and swearing, "major"
	/// emotional breaks are now also possible (going hostile, burnout, changing driving state, accelerating
	/// directly forward to ram through, etc.).
	private float stopped_driver_patience = 60.0f;

	///Heading at which you were travelling when you turned on your signal (used for distance-cancel mode)
	private float _indicator_activation_heading = 0.0f;
		
	///Dictionary of all drivers that are currently being held up by the player and the current time remaining
	/// until they become frustrated.  If a driver becomes frustrated, he will perform an "emotional break", which
	/// can be honking his horn, changing driving style, deciding to barge through, turning hostile, etc.  if only
	/// a minor break is called for, they will resume waiting with one quarter patience.
	/// Anyone who is forced to abandon their vehicle is culled from the list.
	/// Males are twice as likely as females to engage in a hostile break, but hostile breaks are still rare.
	private Dictionary<Ped, float> _driver_patience = new Dictionary<Ped, float>();
		
	private List<Ped> _frustrated_drivers = new List<Ped>();

	private bool _indicator_left_on = false;
	private bool _indicator_right_on = false;
	private bool _hazards_on = false;
		
	public bool IsValidVehicleForIndicators(Vehicle veh) {
		if(!Core.IsPlayerDriving) return false;
		if(veh.Model.IsBicycle) return false;
		if(veh.Model.IsHelicopter) return false;
		if(veh.Model.IsPlane) return false;
		return true;
		}

	private void RealismIndicatorsKeyDown(object sender, KeyEventArgs parameters) {
		if(!IsValidVehicleForIndicators(Game.Player.Character.CurrentVehicle)) return;
		if(parameters.KeyCode == indicator_left_key) {
			if(Core.MatchModifiers(indicator_left_metakey, parameters))
				{
				bool indicator = _indicator_right_on;
				if(indicator_mode_single_tap) indicator = _indicator_left_on;

				if(!indicator) SetIndicatorOn(Indicator.LEFT);
				else CancelIndicators();
				}
			}
		
		if(parameters.KeyCode == indicator_right_key) {
			if(Core.MatchModifiers(indicator_right_metakey, parameters)) {
				bool indicator = _indicator_left_on;
				if(indicator_mode_single_tap) indicator = _indicator_right_on;

				if(!indicator) SetIndicatorOn(Indicator.RIGHT);
				else CancelIndicators();
				}
			}
			
		if(parameters.KeyCode == hazard_key) {
			if(Core.MatchModifiers(hazard_metakey, parameters)) {
				SetHazards(!_hazards_on);
				}
			}
		}

	private void RealismIndicatorsTick(object sender, EventArgs parameters) {
			
		}

	private bool ShouldDriverYieldLongitudinal(Ped driver) {
		if(!Core.IsPlayerDriving) return false;
		if(!driver.IsInVehicle()) return false;
		if(HazardsOn) {
			if((hazard_yield_mode_on & IndicatorsYieldMode.LONGITUDINAL_YIELD) != 0) {
				if(World.GetDistance(driver.CurrentVehicle.Position, Game.Player.Character.CurrentVehicle.Position) <= hazard_longitudinal_range) return true;
				}
			}
		else {
			if((hazard_yield_mode_off & IndicatorsYieldMode.LONGITUDINAL_YIELD) != 0) {
				if(World.GetDistance(driver.CurrentVehicle.Position, Game.Player.Character.CurrentVehicle.Position) <= hazard_longitudinal_range) return true;
				}
			}
		return false;
		}

	private bool ShouldDriverYieldPerpendicular(Ped driver) {
		if(!Core.IsPlayerDriving) return false;
		if(!driver.IsInVehicle()) return false;
		Vehicle veh = driver.CurrentVehicle;

		IndicatorsYieldMode mode;

		if(HazardsOn) mode = hazard_yield_mode_on;
		else mode = hazard_yield_mode_off;

		if((mode & IndicatorsYieldMode.ALL_YIELD) != IndicatorsYieldMode.ALL_YIELD && !IsVehiclePerpendicular(veh)) return false;
		if((mode & IndicatorsYieldMode.PERPENDICULAR_YIELD) != 0) {
			if(World.GetDistance(driver.CurrentVehicle.Position, Game.Player.Character.CurrentVehicle.Position) <= hazard_perpendicular_range) return true;
			}
		return false;
		}

	private bool IsVehicleLongitudinal(Vehicle veh) {
		Vehicle player_veh = Game.Player.Character.CurrentVehicle;
			
		Vector3 upperleft, lowerright;
		player_veh.Model.GetDimensions(out upperleft, out lowerright);
		//float width = lowerright.X - upperleft.X;
		//upperleft.X = width * 0.25f;
		//lowerright.X = width * 0.75f;
		upperleft.Y += lowerright.Y; //use distance from rear bumper
		lowerright.Y += hazard_longitudinal_range;
		Core.MapRectangle rect = new Core.MapRectangle(upperleft, lowerright, player_veh.ForwardVector.ToHeading());
		if(rect.PointWithin(veh.Position)) return true;
		return false;
		}
	private bool IsVehiclePerpendicular(Vehicle veh) {
		Vehicle player_veh = Game.Player.Character.CurrentVehicle;
		float heading = veh.GetOffsetInWorldCoords(player_veh.Position).ToHeading();

		if(heading < 30) return false;
		if(heading > 60 && heading < 255) return false;
		if(heading > 285) return false;

		return true;
		}
	private bool IsVehicleInMergeZone(Vehicle veh, Indicator zone) {
		return false;
		}

	private bool ShouldDriverYieldMerge(Ped driver) {
		return false;
		}


	///Activate turn signal indicators
	private void SetIndicatorOn(Indicator side) {
		if(!_hazards_on) {
			Game.Player.Character.CurrentVehicle.LeftIndicatorLightOn = (side == Indicator.LEFT);
			Game.Player.Character.CurrentVehicle.RightIndicatorLightOn = (side == Indicator.RIGHT);
			}
		if(side == Indicator.RIGHT) {
			_indicator_right_on = true;
			_indicator_left_on = false;
			}
		else {
			_indicator_right_on = false;
			_indicator_left_on = true;
			}
		}
	private void CancelIndicators() {
		if(!_hazards_on) {
			Game.Player.Character.CurrentVehicle.LeftIndicatorLightOn = false;
			Game.Player.Character.CurrentVehicle.RightIndicatorLightOn = false;
			}
		_indicator_left_on = _indicator_right_on = false;
		}
	private void SetHazards(bool on_state) {
		_hazards_on = on_state;
		Game.Player.Character.CurrentVehicle.LeftIndicatorLightOn = _hazards_on;
		Game.Player.Character.CurrentVehicle.RightIndicatorLightOn = _hazards_on;
		if(!on_state) {
			if(_indicator_left_on) Game.Player.Character.CurrentVehicle.LeftIndicatorLightOn = true;
			if(_indicator_right_on) Game.Player.Character.CurrentVehicle.RightIndicatorLightOn = true;
			}
		}

	private void HandleDriverFrustration(float elapsed) {
		foreach(KeyValuePair<Ped, float> item in _driver_patience) {
			Ped driver = item.Key;
			float patience = item.Value - elapsed;
			if(patience < 0 && !EmotionalBreak(driver)) {
				_driver_patience[driver] = (0.5f + Core.RandFloat()) * stopped_driver_patience / 4;
				}
			}
		}

	///Performs an emotional break, and then returns true if the driver has suffered a point-of-no-return outburst 
	/// of road rage (will smash through, or will exit vehicle and attack), false if they will still wait.
	private bool EmotionalBreak(Ped driver) {
		return false;
		}

}

}
