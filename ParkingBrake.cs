using GTA;
using GTA.Native;
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using XInputDotNetPure;
using System.Linq;

namespace RealismVehicles
{

///<remarks>
///<para>This module is the Nintendo Hard setting for the parking brakes.</para>
///<para>Vehicles cannot easily use their parking brakes to perform J-turns.  Vehicles with footbrakes must push down
/// the brake to lock the parking brake when in motion, which makes timing the brake difficult as you will have to
/// carefully time your push of the brake slightly before making the turn.  They therefore take talent to drive
/// correctly.</para>
///<para>Dash-mounted pullbrakes, which are released by pulling on the brake again after it has been fully extended,
/// are also considered foot-operated brakes as they behave similarly.</para>
///<para>All models were meticulously researched versus their real equivalents to verify whether they had
/// handbrakes, footbrakes, or electronic brakes, by referencing official statistics or in a worst case scenario
/// by viewing interior photographs.</para>
///<para>Note that I deliberately avoided whether the in-game model had a handbrake, as most of them simply recycle the
/// same interior model as one or more other vehicles (and this won't do for a mod about realism).</para>
///<para>This is probably the greatest amount of research into parking brakes that any one human being has ever done in
/// the course of hobby research, so that's... that's something.</para>
///</remarks>
public class ParkingBrake : Script
{
	public ParkingBrake() {
		Interval = 100;
		if(parking_brake_enabled) {
			Tick += ParkingBrakeTick;
			}
		Core.OnProcessVehicle += ParkingBrakeOnProcessVehicle;
		Core.OnCleanupVehicle += ParkingBrakeOnCleanupVehicle;
		}

	public enum ParkingBrakeResistanceMode
		{
		///<summary>Vehicle cannot move at all while the parking brake is locked.</summary>
		LOCK = 1,
		///<summary>Vehicle applies continuous burnout while the parking brake is locked.</summary>
		BURNOUT = 2,
		///<summary>Vehicle rapidly triggers the brake on and off</summary>
		RESISTANCE = 3
		}

	///// PARKING BRAKE /////
	// * Disabled by default.
	// * When holding down the Handbrake control while the vehicle is stopped, the parking brake will be locked on
	//   for that vehicle.
	// * Before the vehicle can be driven, hold the Handbrake control briefly to disengage the parking brake.
	// * Parked cars that spawn naturally will have their parking brake on 40 percent of the time, by default.
	// * This is solely intended as a difficulty feature to catch you unawares in a panic scenario while stealing
	//   a parked car to make a getaway.  Being unaware of it will ruin your fun.  Do not enable unless you are
	//   certain that you want the added difficulty.
	// * Vehicles with handbrakes can be tapped to apply quick braking, but will lock if held too long.
	// * Vehicles with foot- or column-operated parking brakes behave similarly to handbrakes, but cannot be tapped;
	//    they do not function until they lock.
	// * Vehicles with hold-button electronic parking brakes follow vanilla behaviour -- they can drift and will
	//    only lock if the vehicle is stopped.
		
	///<summary>Enable/disable this module.</summary>
	///<remarks>Not conceptually identical to setting all of the <see cref="UseLockingFootbrakes"/>,
	/// <see cref="UseLockingHandbrakes"/>, and <see cref="UseLockingElectronicBrakes"/> settings to
	/// <see cref="Core.EnableState.Off"/>, because this also disables the ability to lock a parking brake while
	/// stopped, whereas those settings are more concerned about what happens when the vehicle is moving.</remarks>
	public bool Enabled {
		get { return parking_brake_enabled; }
		set { parking_brake_enabled = true; }
		}

	///<summary>Maximum speed at which the handbrake control will lock the parking brakes, on conventional
	/// models.</summary>
	///<remarks>Because the parking brake control is the Handbrake control, this should be set to a low value.
	/// This value is ignored for vehicles with footbrakes, if the footbrakes setting is enabled; footbrakes on
	/// applicable models can be activated at ''any'' speed.</remarks>
	public float SpeedLimit {
		get { return parking_brake_speed_limit; }
		set { parking_brake_speed_limit = value; }
		}
	
	///<summary>Which level of footbrake simulation to represent in the game, i.e., whether vehicles with locking
	/// footbrakes will lock at speed or not if the Handbrake control is held too long.</summary>
	///<remarks>Off = No footbrakes (lock only when parking), Select = as On, Blacklist = As Select
	/// but blacklisted models have handbrakes instead, On = all whitelisted models can lock footbrakes.</remarks>
	public Core.EnableState UseLockingFootbrakes {
		get { return footbrake_simulation; }
		set { footbrake_simulation = value; }
		}
	
	///<summary>Which level of handbrake simulation to represent in the game, i.e., whether vehicles with locking
	/// pull-up handbrakes will lock at speed or not.</summary>
	///<remarks>Off = No footbrakes (lock only when parking), Select = as On, Blacklist = As Select
	/// but blacklisted models have footbrakes instead, On = all whitelisted models can lock handbrakes.</remarks>
	public Core.EnableState UseLockingHandbrakes {
		get { return handbrake_simulation; }
		set { handbrake_simulation = value; }
		}
	
	///<summary>Which level of electronic brake simulation to represent in the game, i.e., whether vehicles with
	/// hold-button electronic brakes will lock at speed (Blacklist), whether vehicles with either hold- or lock-button
	/// electronic brakes can be locked at speed (On), or whether hold-button electronic brakes can drift
	/// (Select).</summary>
	///<remarks><para>Off = No locking electronic brakes (lock only when parking), Select = Push-button electronic brakes
	/// CANNOT lock at speed, Blacklist = Push-button electronic brakes CAN lock at speed, On = ALL electronic brakes
	/// can lock at speed including lock-button (which normally lock only when parking).</para>
	///<para>Note that these values are slightly different than <see cref="UseLockingHandbrakes"/> or
	/// <see cref="UseLockingFootbrakes"/>.</para></remarks>
	public Core.EnableState UseLockingElectronicBrakes {
		get { return electronic_brake_simulation; }
		set { electronic_brake_simulation = value; }
		}
	
	///<summary>Whether you can still tap the handbrake control in cars with a footbrake (false), or whether the handbrake
	/// only kicks in when the footbrake is fully engaged (true).  If false, footbrakes are identical to handbrakes.
	/// This setting does not apply to electronic parking brake vehicles or handbrake vehicles.</summary>
	public bool FootbrakeDisablesHandbrakeControl {
		get { return use_delayed_footbrakes; }
		set { use_delayed_footbrakes = value; }
		}
	
	internal bool parking_brake_enabled = true;

	internal float parking_brake_speed_limit = 1.5f; //m/s

	internal Core.EnableState footbrake_simulation = Core.EnableState.On;
	internal Core.EnableState handbrake_simulation = Core.EnableState.Off;
	internal Core.EnableState electronic_brake_simulation = Core.EnableState.Select;

	///<summary>Do motorcycles have parking brakes? (Most don't, if you didn't know. First gear, park uphill,
	/// problem solved.)</summary>
	internal bool motorcycles_have_parking_brakes = false;

	///<summary>If using the Selected Models setting for the footbrakes mode, you can optionally whitelist
	/// handbrakes for specific model *classes* even if they actually would have footbrakes in reality, for
	/// better playability.</summary>
	internal bool class_footbrake_overrides = false;

	internal List<VehicleClass> classes_with_footbrake_overrides = new List<VehicleClass> {
		VehicleClass.Sports
		};

	///<summary>Whether you can still tap the handbrake control in cars with a footbrake (false), or whether the handbrake
	/// only kicks in when the footbrake is fully engaged (true).  Does not apply to handbrake or electronic brake
	/// vehicles.</summary>
	internal bool use_delayed_footbrakes = true;

	///<summary>Whether cars with push-lock electronic parking brakes can be activated at speed (true) or require
	/// the vehicle to stop before the Handbrake can be used (false). If false, vehicles with push-lock electronic
	/// brakes (as opposed to hold-button electronic brakes) cannot be drifted!</summary>
	internal bool tap_all_electronic_brakes = true;

	///<summary>Exhaustive list of all vehicle models which have pull-locking handbrakes.</summary>
	///<remarks>I was actually quite shocked to discover that there are more vehicles in GTA V with non-handbrakes
	/// than there are with handbrakes.  I've only ever owned vehicles that have handbrakes in reality and the
	/// mere thought of having to do without in a personal vehicle is depressing. =)  (I've driven plenty of fleet
	/// vehicles with footbrakes, though.)</remarks>
	internal List<VehicleHash> handbrake_models = new List<VehicleHash>() {
		VehicleHash.Alpha, VehicleHash.Ardent, VehicleHash.Asea, VehicleHash.Asea2,VehicleHash.Banshee, VehicleHash.Banshee2,
		VehicleHash.BfInjection, VehicleHash.Bifta, VehicleHash.BJXL, VehicleHash.Blista, VehicleHash.Blista2,
		VehicleHash.Blista3, VehicleHash.Bodhi2, VehicleHash.Brawler, VehicleHash.BType, VehicleHash.BType2,
		VehicleHash.BType3, VehicleHash.Bullet, VehicleHash.Caddy, VehicleHash.Caddy2, VehicleHash.Carbonizzare,
		VehicleHash.Casco, VehicleHash.Cheetah, VehicleHash.Cheetah2, VehicleHash.Comet2, VehicleHash.Comet3, VehicleHash.Coquette2,
		VehicleHash.Coquette3, VehicleHash.Crusader, VehicleHash.Dominator, VehicleHash.Dominator2, VehicleHash.Elegy,
		VehicleHash.Elegy2, VehicleHash.EntityXF, VehicleHash.Feltzer3, VehicleHash.Forklift, VehicleHash.Fugitive,
		VehicleHash.Futo, VehicleHash.GP1, VehicleHash.Hotknife, VehicleHash.Infernus2, VehicleHash.Ingot,
		VehicleHash.Intruder, VehicleHash.Issi2, VehicleHash.Issi3, VehicleHash.Jester3, VehicleHash.Kuruma,
		VehicleHash.Kuruma2, VehicleHash.Mamba, VehicleHash.Mesa, VehicleHash.Mesa2, VehicleHash.Mesa3,
		VehicleHash.Minivan, VehicleHash.Minivan2, VehicleHash.Mower, VehicleHash.Osiris, VehicleHash.Panto,
		VehicleHash.Penetrator, VehicleHash.Penumbra, VehicleHash.Pigalle, VehicleHash.Prairie, VehicleHash.RatLoader,
		VehicleHash.RatLoader2, VehicleHash.Rhapsody, VehicleHash.Ruston, VehicleHash.Sentinel, VehicleHash.Sentinel2,
		VehicleHash.Sultan, VehicleHash.SultanRS, VehicleHash.Tractor, VehicleHash.Tractor2, VehicleHash.Tractor3,
		VehicleHash.Turismor, VehicleHash.Verlierer2, VehicleHash.Warrener,
		//IVPack
		(VehicleHash)0x705A3E41/*CABBY*/, (VehicleHash)0xFBFD5B62/*CHAVOS*/, (VehicleHash)0x1C18FCE2/*CHEETAH3*/,
		(VehicleHash)0x98F65A5E/*COQUETTE4*/, (VehicleHash)0x09B56631/*DF8*/, (VehicleHash)0x971AB25B/*DOUBLE2*/,
		(VehicleHash)0xBE9075F1/*FELTZER*/, (VehicleHash)0x3A196CEA/*FEROCI*/, (VehicleHash)0x3D285C4A/*FEROCI2*/,
		(VehicleHash)0x255FC509/*FORTUNE*/, (VehicleHash)0xA6297CC8/*FUTO2*/, (VehicleHash)0xEB9F21D3/*HAKUMAI*/,
		(VehicleHash)0x07D10BDC/*PINNACLE*/, (VehicleHash)0x04F48FC4/*REBLA*/,
		(VehicleHash)0xAF1FA439/*SENTINEL4*/, (VehicleHash)0x3404691C/*SULTAN2*/,
		(VehicleHash)0x6C9962A9/*SUPERGT*/, (VehicleHash)0x8EF34547/*TURISMO*/,
		(VehicleHash)0x1956C3C8/*TYPHOON*/, (VehicleHash)0x5B73F5B7/*URANUS*/,
		(VehicleHash)0xDD3BD501/*VINCENT*/,
		//WOV

		//VWE
		
		//Non-realistic, for playability
		VehicleHash.Buffalo, VehicleHash.Buffalo2, VehicleHash.Buffalo3, VehicleHash.FBI
		};

	///<summary>List of modelhashes which have hold-button electronic brakes that can be drifted (high-performance
	/// sportscars) and which lock only at minimal speed.</summary>
	internal List<VehicleHash> hold_electronic_brake_models = new List<VehicleHash>() {
		VehicleHash.Coquette, VehicleHash.Exemplar, VehicleHash.F620, VehicleHash.Felon, VehicleHash.Felon2,
		VehicleHash.Feltzer2, VehicleHash.Furoregt,
		VehicleHash.Huntley, VehicleHash.Infernus, VehicleHash.ItaliGTB, VehicleHash.ItaliGTB2, VehicleHash.Jackal,
		VehicleHash.Jester, VehicleHash.Jester2,
		VehicleHash.Khamelion, VehicleHash.Limo2, VehicleHash.Massacro,
		VehicleHash.Massacro2, VehicleHash.Nero, VehicleHash.Nero2, VehicleHash.Ninef, VehicleHash.Ninef2,
		VehicleHash.RapidGT, VehicleHash.RapidGT2, 
		VehicleHash.Specter, VehicleHash.Specter2, VehicleHash.Surano, VehicleHash.T20,
		VehicleHash.Tempesta, VehicleHash.Turismor, VehicleHash.Vacca, VehicleHash.Vagner, VehicleHash.Zentorno,
		//IVPack
		(VehicleHash)0xF3E6B70E/*SCHAFGTR*/
		//VWE
		};
	
	///<summary>List of modelhashes which have push-button locking electronic brakes which cannot be drifted (luxury
	/// cars and electric vehicles), unless <see cref="tap_all_electronic_brakes"/> is true.  These vehicles have no
	/// "handbrake" feature -- the parking brake only works when the vehicle is stopped.</summary>
	internal List<VehicleHash> lock_electronic_brake_models = new List<VehicleHash>() {
		VehicleHash.Adder, VehicleHash.Dubsta, VehicleHash.Dubsta2, VehicleHash.Dubsta3, VehicleHash.Cognoscenti,
		VehicleHash.Cognoscenti2, VehicleHash.Cog55, VehicleHash.Cog552, VehicleHash.CogCabrio, VehicleHash.Dilettante,
		VehicleHash.Fugitive, VehicleHash.Landstalker, VehicleHash.Oracle, VehicleHash.Oracle2, VehicleHash.Radi,
		VehicleHash.Rocoto, VehicleHash.Schafter2, VehicleHash.Schafter3, VehicleHash.Schafter4, VehicleHash.Schafter5,
		VehicleHash.Schafter6, VehicleHash.Schwarzer, VehicleHash.Serrano, VehicleHash.Superd, VehicleHash.Tailgater,
		VehicleHash.Voltic, VehicleHash.Windsor, VehicleHash.Windsor2, VehicleHash.Zion, VehicleHash.Zion2,
		//IVPack
		(VehicleHash)0x61A3B9BA/*SUPERD2*/
		};

	///<summary>Percentage chance (0.0-100.0) of any parked, unoccupied vehicle having its parking brake set.  The
	/// default is set to an educated North American guess of 40.0.</summary>
	///<remarks><para>I dearly, dearly wish this was higher... in North America, most idiots only use the Park gear
	/// on their automatic transmissions.  (If you're American, try going to a hilly parking lot and just sitting
	/// and watching the number of cars which bounce off the pawl after the driver lets go of the service brakes.
	/// Then try to contain your tears.)  I found the majority of our fleet vehicles without the parking brakes
	/// set, even at our old office (on a hill) -- and those are the professional drivers, which bodes ill for the
	/// rest of the population...</para>
	///<para>Do you know how much is holding your car when in Park?  A little tiny pin called a "pawl".  It notches
	/// into a hole in the transmission gear.  Do you know how much is holding your car when the parking brake is
	/// on?  Your rear brakes.  You know, the things that are proven to be at least halfway capable of slowing and
	/// stopping your car whenever you change speed.</para>
	///<para>Ahem.</para></remarks>
	internal float parking_brake_chance = 40.0f;

	///<summary>Minimum average time between scans for locking parked vehicles.</summary>
	///<remarks>A small random factor (+/- 50%) is added to distribute script load.  Scans are only ever made while
	/// on foot.</remarks>
	internal float parking_scan_delay = 4.7f;

	///<summary>Amount of time (in seconds) the Handbrake control needs to be held in order to set the parking
	/// brake in vehicles with a handbrake.</summary>
	internal float handbrake_set_time = 0.6f;

	///<summary>Amount of time (in seconds) the Handbrake control needs to be held in order to set the parking
	/// brake in vehicles with a footbrake.</summary>
	internal float footbrake_set_time = 0.15f;

	///<summary>Amount of time (in seconds) the Handbrake control needs to be held in order to release the parking
	/// brake.</summary>
	internal float parking_brake_release_time = 0.15f;

	///<summary><see cref="ParkingBrakeResistanceMode"/></summary>
	internal ParkingBrakeResistanceMode parking_brake_hold_mode = ParkingBrakeResistanceMode.RESISTANCE;

	///<summary>Flips the vehicle's handbrake on and off every couple of frames while the mode is set to
	/// <see cref="ParkingBrakeResistanceMode.RESISTANCE"/>, to allow the vehicle to move even while the handbrake
	/// is applied -- but very ineffectively.</summary>
	///<remarks>As a bonus, it causes the handbrake icon to blink on the vehicle model's dashboard.</remarks>
	private int _resistance_frame = 0;
		
	///<summary>Number of frames to wait before auto-applying resistance.  Higher is less resistance.</summary>
	private const int _parking_brake_resistance = 3;
	///<summary>Number of frames to hold brakes while applying resistance.  Higher is more resistance.</summary>
	private const int _parking_brake_resist_time = 2;

	///<summary>List of vehicles in the game which have been checked.</summary>
	private List<int> _parking_checked_vehicles = new List<int>();
	///<summary>List of vehicles in the game which have their parking brakes set.</summary>
	private List<int> _parked_vehicles = new List<int>();

	///<summary>Flag to determine whether player has changed state -- entering/exiting vehicle.</summary>
	private bool _was_driving_vehicle = false;
	///<summary>Flag to determine whether player has changed state -- starting/stopping engine.</summary>
	private bool _parking_was_vehicle_running = true;

	///<summary>Does the player's current vehicle use a footbrake/pushbrake/any other brake with a release handle?</summary>
	private bool _player_veh_footbrake = false;
	///<summary>Does the player's vehicle have a push-to-drift electronic brake rather than a pure footbrake or handbrake?</summary>
	private bool _player_veh_holdbutton = false;
	///<summary>Does the player's vehicle have a parking-only locking electronic brake rather than a pure footbrake or handbrake?</summary>
	private bool _player_veh_lockbutton = false;
	
	///<summary>How long has player been holding the Handbrake control?</summary>
	private float _handbrake_control_held_timer = 0.0f;
	///<summary>Is player still holding the Handbrake control since the parking brake state was changed? Prevents
	/// undesirable behaviour of both setting and releasing the brake with a single press of the key (must release key and press again).</summary>
	private bool _handbrake_control_held = false;
		
	///<summary>Sets the parking brake on in the player's specified vehicle.</summary>
	public void SetParkingBrake(Vehicle veh, bool lock_handbrake_control = false) {
		if(lock_handbrake_control) {
			if(parking_brake_hold_mode == ParkingBrakeResistanceMode.BURNOUT)
				Function.Call(Hash.SET_VEHICLE_BURNOUT, veh, lock_handbrake_control);
			else
				veh.HandbrakeOn = true;
			}
		
		if(Game.Player.Character.CurrentVehicle == veh) {
			if(_player_veh_holdbutton)      UI.Notify("~g~Electronic parking brake set.~s~");
			else if(_player_veh_lockbutton) UI.Notify("~g~Electronic parking brake set.~s~");
			else if(_player_veh_footbrake)  UI.Notify("~g~Parking brake set.~s~");
			else                            UI.Notify("~g~Handbrake locked and set.~s~");
			}
		
		if(!_parking_checked_vehicles.Contains(veh.Handle)) _parking_checked_vehicles.Add(veh.Handle);
		if(!_parked_vehicles.Contains(veh.Handle)) _parked_vehicles.Add(veh.Handle);
		}
	public void ReleaseParkingBrake(Vehicle veh) {
		veh.HandbrakeOn = false;
		Function.Call(Hash.SET_VEHICLE_BURNOUT, veh, false);
			
		if(Game.Player.Character.CurrentVehicle == veh) {
			if(_player_veh_holdbutton)      UI.Notify("~g~Electronic parking brake released.~s~");
			else if(_player_veh_lockbutton) UI.Notify("~g~Electronic parking brake released.~s~");
			else if(_player_veh_footbrake)  UI.Notify("~g~Parking brake released.~s~");
			else                            UI.Notify("~g~Handbrake unlocked and released.~s~");
			}
			
		_parked_vehicles.Remove(veh.Handle);
		if(!_parking_checked_vehicles.Contains(veh.Handle)) _parking_checked_vehicles.Add(veh.Handle);
		}
	
	private void ParkingBrakeOnCleanupVehicle(object sender, ProcessVehicleEventArgs e) {
		Vehicle condemned = (Vehicle)sender;
		int condemned_handle = condemned.Handle;
		_parking_checked_vehicles.Remove(condemned_handle);
		_parked_vehicles.Remove(condemned_handle);
		}
	
	private void ParkingBrakeOnProcessVehicle(object sender, ProcessVehicleEventArgs e) {
		Vehicle parked_veh = e.veh;
		VehicleData vdata = e.vdata;

		if(_parking_checked_vehicles.Contains(parked_veh.Handle)) return;
		if(!HasParkingBrake(parked_veh)) {
			if(!_parking_checked_vehicles.Contains(parked_veh.Handle)) _parking_checked_vehicles.Add(parked_veh.Handle);
			return;
			}
		if(parked_veh.Driver.Exists() || parked_veh.PassengerCount > 0) return;
		if(!parked_veh.IsAlive || !parked_veh.Exists()) return;
		
		if((Core.RandPercentage()) <= parking_brake_chance) _parked_vehicles.Add(parked_veh.Handle);
		if(!_parking_checked_vehicles.Contains(parked_veh.Handle)) _parking_checked_vehicles.Add(parked_veh.Handle);
		}
	
	private void ParkingBrakeTick(object sender, EventArgs e) {
		if(!Core.IsPlayerDriving) {
			if(_was_driving_vehicle) {
				Interval = 100;
				_was_driving_vehicle = _player_veh_footbrake = _player_veh_holdbutton = _player_veh_lockbutton = false;
				
				Vehicle last_vehicle = Game.Player.LastVehicle;
				last_vehicle.HandbrakeOn = false; //This clears the lock on the vehicle's handbrake control, allowing AI to drive it if needed

				//Can hold Handbrake while exiting vehicle to lock the brake, even if you didn't lock it prior to exiting
				if(Entity.Exists(last_vehicle) && !_parked_vehicles.Contains(last_vehicle.Handle)) {
					if(Core.ControlHeld(GTA.Control.VehicleHandbrake)) {
						SetParkingBrake(last_vehicle);
						}
					}
				}
			return;
			}
		
		Vehicle veh = Game.Player.Character.CurrentVehicle;
		if(!HasParkingBrake(veh)) return;
		Interval = 0;

		if(!_was_driving_vehicle) {
			_was_driving_vehicle = true;

			if(VehicleUsesFootbrake(veh)) _player_veh_footbrake = true;
			else if(hold_electronic_brake_models.Contains((VehicleHash)veh.Model.Hash)) _player_veh_holdbutton = true;
			else if(lock_electronic_brake_models.Contains((VehicleHash)veh.Model.Hash)) _player_veh_lockbutton = true;
				
			if(_parked_vehicles.Contains(veh.Handle)) {
				veh.HandbrakeOn = true;
				string brake_warning = "";
				if(_player_veh_holdbutton || _player_veh_lockbutton) brake_warning = "electronic parking brake";
				else if(_player_veh_footbrake) brake_warning = "parking brake";
				else                           brake_warning = "handbrake";
				UI.Notify("The " + brake_warning + " is set. Hold Handbrake to release.");
				return;
				}
			_parking_checked_vehicles.Add(veh.Handle);
			}
			
		if(_parked_vehicles.Contains(veh.Handle)) {
			//In Resistance mode, the parking brake is rapidly released and reapplied to allow the engine to fight against the
			// parking brake as well as to maintain traction, which is a more realistic outcome of driving with the parking
			// brake on.  (It is frame rate dependent, but this dependency is highly unlikely to be noticed.)
			if(parking_brake_hold_mode == ParkingBrakeResistanceMode.RESISTANCE) {
				//The condition below is a bit cryptic, but it's essentially an XOR -- it evaluates true if the player is pressing
				// either control, but not if pressing both or neither.
				if(Core.ControlHeld(GTA.Control.VehicleAccelerate) != Core.ControlHeld(GTA.Control.VehicleBrake)) {
					if(++_resistance_frame >= _parking_brake_resistance + _parking_brake_resist_time + (int)Math.Sqrt(veh.Speed)) {
						_resistance_frame = 0;
						veh.HandbrakeOn = true;
						}
					else if(_resistance_frame >= _parking_brake_resist_time + (int)Math.Sqrt(veh.Speed)) veh.HandbrakeOn = false;
					}
				else veh.HandbrakeOn = true;
				}

			if(_handbrake_control_held) {
				if(!Core.ControlHeld(GTA.Control.VehicleHandbrake)) _handbrake_control_held = false;
				return;
				}
			else if(_handbrake_control_held_timer < parking_brake_release_time && Core.ControlHeld(GTA.Control.VehicleHandbrake)) {
				if(Game.FPS > 0) _handbrake_control_held_timer += 1.0f / (float)(Game.FPS+1);
				}
			else if(Core.ControlHeld(GTA.Control.VehicleHandbrake)) {
				_handbrake_control_held_timer = 0.0f;
				_handbrake_control_held = true;
				ReleaseParkingBrake(veh);
				}
			else {
				_handbrake_control_held_timer = 0.0f;
				}
			}
		else {
			bool can_handbrake_lock = veh.IsStopped || veh.Speed < parking_brake_speed_limit;
			if(_player_veh_holdbutton) {
				can_handbrake_lock = can_handbrake_lock || UseLockingElectronicBrakes == Core.EnableState.On || UseLockingElectronicBrakes == Core.EnableState.Blacklist;
				}
			else if(_player_veh_footbrake) {
				can_handbrake_lock = can_handbrake_lock || UseLockingFootbrakes == Core.EnableState.On || UseLockingFootbrakes == Core.EnableState.Select;
				if(use_delayed_footbrakes) Game.DisableControlThisFrame(0, GTA.Control.VehicleHandbrake);
				}
			else if(_player_veh_lockbutton) {
				//Note that "Select" is exempted here: lock-button electronic brakes will ONLY allow locking when stopped, unless set to On
				can_handbrake_lock = can_handbrake_lock || UseLockingElectronicBrakes == Core.EnableState.On;
				}
			else { //handbrake
				can_handbrake_lock = can_handbrake_lock || UseLockingHandbrakes == Core.EnableState.On || UseLockingHandbrakes == Core.EnableState.Select;
				}
				
			if(_handbrake_control_held) {
				if(!Core.ControlHeld(GTA.Control.VehicleHandbrake)) _handbrake_control_held = false;
				return;
				}
			else if(can_handbrake_lock) {
				//Handbrakes set more slowly than footbrakes
				float hold_time = (!_player_veh_footbrake && !_player_veh_holdbutton) ? handbrake_set_time : footbrake_set_time;
				if(_handbrake_control_held_timer < hold_time && Core.ControlHeld(GTA.Control.VehicleHandbrake)) {
					if(Game.FPS > 0) _handbrake_control_held_timer += (1.0f / (float)Game.FPS);
					if(_player_veh_footbrake) Game.DisableControlThisFrame(0, GTA.Control.VehicleHandbrake);
					}
				else if(Core.ControlHeld(GTA.Control.VehicleHandbrake)) {
					_handbrake_control_held_timer = 0.0f;
					_handbrake_control_held = true;
					SetParkingBrake(veh, true); //also lock control, as player is still inside vehicle (veh.HandbrakeOn)
					}
				else {
					_handbrake_control_held_timer = 0.0f;
					}
				}
			}
		}

	private bool HasParkingBrake(Vehicle veh) {
		if(veh.Model.IsCar) return true;
		if(veh.Model.IsBike || veh.Model.IsQuadbike) return motorcycles_have_parking_brakes;
		return false;
		}

	private bool VehicleUsesFootbrake(Vehicle veh) {
		if(footbrake_simulation == Core.EnableState.Off) return false;
		if(class_footbrake_overrides && classes_with_footbrake_overrides.Contains(veh.ClassType)) return false;
		if(handbrake_models.Contains((VehicleHash)veh.Model.Hash)) return false;
		if(hold_electronic_brake_models.Contains((VehicleHash)veh.Model.Hash)) return false;
		if(lock_electronic_brake_models.Contains((VehicleHash)veh.Model.Hash)) return false;
		return true;
		}

}

}


