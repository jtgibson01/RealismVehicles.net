using GTA;
using GTA.Native;
using GTA.Math;
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using XInputDotNetPure;
using System.Linq;
using System.Text;
using System.IO;

namespace RealismVehicles
{

public sealed class VehicleModItem {
	VehicleHash vehicle_hash;
	VehicleMod mod_type;
	int mod_index;
	
	public VehicleModItem(VehicleHash vhash, VehicleMod mod, int index) {
		vehicle_hash = vhash;
		mod_type = mod;
		mod_index = index;
		}
	public VehicleModItem(long vhash, VehicleMod mod, int index) {
		vehicle_hash = (VehicleHash)vhash;
		mod_type = mod;
		mod_index = index;
		}
	public VehicleModItem(long vhash, int mod, int index) {
		vehicle_hash = (VehicleHash)vhash;
		mod_type = (VehicleMod)mod;
		mod_index = index;
		}
	public VehicleModItem(VehicleHash vhash, int mod, int index) {
		vehicle_hash = vhash;
		mod_type = (VehicleMod)mod;
		mod_index = index;
		}
}

public sealed class ProcessVehicleEventArgs : EventArgs {
	public Vehicle veh;
	public VehicleData vdata;
	public int frames;

	public ProcessVehicleEventArgs(Vehicle veh, VehicleData vdata, int frames_since_last_process = 1) {
		this.veh = veh;
		this.vdata = vdata;
		this.frames = frames_since_last_process;
		}
}

public delegate void ProcessVehicleHandler(object sender, ProcessVehicleEventArgs e);

public sealed class VehicleData {
	///<summary>The current temperature in degrees Kelvin above an assumed STP of ~300 K/1000 kPa.</summary>
	///<remarks>Cars that are newly spawned with the engine running are assumed to be at operating temperature.
	///<para>Cars that are newly spawned with the engine off are assumed to be at environment temperature.</para>
	///<para>If the actual environmental temperature is below ~300 K (~25 ºC, ~80 ºF), the engine will start at a
	/// negative temperature, taking longer to warm up.</para></remarks>
	public float EngineTemperature {
		get { return _engine_temperature; }
		set { _engine_temperature = value; }
		}
	
	///<summary>Amount of temporary damage this engine has suffered from overheating.  Does *NOT* include the permanent damage that is
	/// also suffered from severe overheating, if any.</summary>
	///<remarks>When the vehicle cools, overheat damage will also recover.</remarks>
	public float TemporaryOverheatDamage {
		get { return _overheating_damage; }
		set { _overheating_damage = (float)Math.Max(0, value); }
		}
	
	///<summary>Has the engine ever reached the operating temperature?</summary>
	public bool OperatingTemperatureReached {
		get { return _warm; }
		set { _warm = value; }
		}
	///<summary>Has active cooling kicked in?</summary>
	public bool IsFanRunning {
		get { return _fan_running; }
		set { _fan_running = value; }
		}
	///<summary>Has the engine begun boiling coolant (above <see cref="Engine.OverheatTemperature"/>)?</summary>
	public bool IsBoilingOver {
		get { return _boiling; }
		set { _boiling = value; }
		}
	///<summary>Has the engine begun burning oil (above <see cref="Engine.BurnOilTemperature"/>)?</summary>
	public bool IsBurningOil {
		get { return _burning; }
		set { _burning = value; }
		}
		
	///<summary>Has the engine begun switched off manually?</summary>
	public bool IsKeyedOff {
		get { return _engine_cutoff; }
		set { _engine_cutoff = value; }
		}
	
	///<summary>The last engine health remembered by the mod (to track whether new damage is suffered or vehicle has been repaired).</summary>
	public float LastEngineHealth {
		get { return _last_known_engine_health; }
		set { _last_known_engine_health = value; }
		}
	public float LastBodyHealth {
		get { return _last_known_body_health; }
		set { _last_known_body_health = value; }
		}
	public float LastTankHealth {
		get { return _last_known_fuel_health; }
		set { _last_known_fuel_health = value; }
		}
	public Vector3 LastPosition {
		get { return _last_known_position; }
		set { _last_known_position = value; }
		}
	
	///<summary>Floating point percent of fuel remaining, from 0.0 to 1.0</summary>
	public float FuelRemaining {
		get { return _fuel_remaining; }
		set { _fuel_remaining = (float)Math.Max(Math.Min(1.0, value), 0.0); }
		}	
	///<summary>Amount of fuel remaining, in litres. Can also be used to set a specific number of litres of fuel in the tank.</summary>
	public float FuelAmount {
		get { return _fuel_remaining * _fuel_capacity; }
		set { _fuel_remaining = (float)Math.Max(Math.Min(1.0, value), 0.0) / _fuel_capacity; }
		}
	///<summary>Read-only. Amount of fuel that the tank will hold, in litres.</summary>
	public float FuelCapacity {
		get { return _fuel_capacity; }
		}
	///<summary>Read-only. Amount of fuel needed to fill the tank (based on its current fill level).</summary>
	public float FuelRequired {
		get { return (1.0f - _fuel_remaining) * _fuel_capacity; }
		}
	
	///<summary>Number of kilometres accumulated since last reset of the trip odometer.</summary>
	public float TripOdometer {
		get { return _trip_odometer; }
		set { _trip_odometer = value; }
		}
	///<summary>Resets the trip odometer.</summary>
	public void ResetTripOdometer() {
		_trip_odometer = 0f;
		}
	
	///<summary>Number of kilometres the vehicle has ever travelled.  Should be converted to whichever units the player prefers.</summary>
	public float Odometer {
		get { return _odometer; }
		set { _odometer = value; }
		}
	
	///<summary>Type of <see cref="Core.TransmissionType"/> that is installed in this vehicle.</summary>
	public Core.TransmissionType TransmissionType {
		get { return _transmission; }
		set { _transmission = value; }
		}
	
	public bool TransmissionAssigned {
		get { return TransmissionType != Core.TransmissionType.NONE; }
		}
	
	public bool Dead {
		get { return _dead; }
		set { _dead = value; }
		}
	
	public bool TemporarilyFireproof {
		get { return _temp_fireproof; }
		}
	public bool TemporaryFireproofing(Vehicle veh, bool enable) {
		if(enable) {
			veh.IsFireProof = _temp_fireproof = true;
			}
		else {
			veh.IsFireProof = _temp_fireproof = false;
			}
		return _temp_fireproof;
		}
	
	public bool HasAdaptiveCruise {
		get { return _has_adaptive_cruise; }
		set { _has_adaptive_cruise = value; }
		}
	public bool HasCruiseAutomaticBraking {
		get { return _has_adaptive_cruise || _has_braking_cruise; }
		set { _has_braking_cruise = value; }
		}
	public bool HasCruiseControl {
		get { return _has_adaptive_cruise || _has_braking_cruise || _has_cruise; }
		set { _has_cruise = value; }
		}
	
	private float _fuel_capacity = 65f;
	private float _fuel_remaining = 1.0f;
	
	private float _engine_temperature = 0f;

	private float _overheating_damage = 0f;
	
	private bool _warm = false;

	private bool _spare_tire = true;
	
	private bool _fan_running = false;

	private bool _engine_cutoff = false;

	private bool _temp_fireproof = false;
	
	private float _last_known_body_health;
	private float _last_known_fuel_health;
	private float _last_known_engine_health;
	private Vector3 _last_known_position;

	private float _odometer = 0f;
	private float _trip_odometer = 0f;

	private bool _boiling = false;
	private bool _burning = false;

	private bool _dead = false;

	private bool _has_cruise = false;
	private bool _has_braking_cruise = false;
	private bool _has_adaptive_cruise = false;

	private string _registered_VIN = "";
	
	private Core.TransmissionType _transmission = Core.TransmissionType.NONE;

	public void KeyOnOff(Vehicle veh, bool on = true) {
		Function.Call(Hash.SET_VEHICLE_ENGINE_ON, veh, on, false, true); //Vehicle veh, bool on, bool immediately, bool override
		veh.IsDriveable = on;
		}
	
	public VehicleData(Vehicle veh) {
		Core.DebugNotify("Constructed new VehicleData for " + veh.FriendlyName + " with handle " + veh.Handle.ToString("x8"), Core.DebugLevel.ALL);

		if(Core.fix_licence_plates && veh.NumberPlate == "46EEK572") {
			string new_plate = Core.RandInt(10,99).ToString() + (char)(Core.RandInt(0,25)+'A') + (char)(Core.RandInt(0,25)+'A') + (char)(Core.RandInt(0,25)+'A') + Core.RandInt(100,999).ToString();
			Core.DebugNotify("Fixed 46EEK572 plate of " + veh.FriendlyName + ", now " + new_plate);
			veh.NumberPlate = new_plate;
			}

		_last_known_engine_health = veh.EngineHealth = Math.Min(veh.EngineHealth, 1000f);
		_last_known_fuel_health = veh.FuelLevel;
		_overheating_damage = 0f;

		_fuel_remaining = 0f;
		_fuel_capacity = 0f;

		if(veh.EngineRunning) {
			_engine_cutoff = false;
			_engine_temperature = Engine.OperatingTemperature;
			_warm = true;
			}
		else {
			_engine_cutoff = true;
			_engine_temperature = Engine.EnvironmentTemperature(veh);
			_warm = false;
			}
		
		//TODO: LOAD ODOMETER DATA BASED ON LICENCE PLATE
		_odometer = Core.RandFloat(35f, 80f);
		//TODO: ADJUST ODOMETER BASED ON STATISTICAL LIKELIHOOD PER MODEL TYPE
		_trip_odometer = Math.Min(1000f, Core.RandFloat(0f, _odometer));
		}
	
	public string GenerateVIN(Vehicle veh) {
		throw new NotImplementedException();
		}
}

public sealed class Core : Script
{
	public Core() {
		Interval = 1;
		Tick += RealismCoreTick;

		BackupLastLog();
		ClearLogFile();
		}
	
	///// ENUMERATIONS /////

	///<summary>Matched enumeration of Manual Transmission mod's transmission shifter modes; the type of user interface the player will use. Do not edit unless MT mod changes.</summary>
	public enum ShifterMode {
		///<summary>MT mod is using paddle-shift/sequential transmission.</summary>
		SEQUENTIAL = 1,
		///<summary>MT mod is using H-shifter/specific-gear transmission</summary>
		HPATTERN = 2,
		///<summary>MT mod is using simulated automatic transmission.</summary>
		AUTOMATIC = 3
		}
	///<summary>Actual transmission types that may be installed into cars; the available mechanical options to the player.</summary>
	public enum TransmissionType {
		///<summary>Vehicle's transmission has not yet been assigned</summary>
		NONE = 0,
		///<summary>Vehicle uses an H-shifter transmission only.  (Might be Sequential if <see cref="Transmission.manual_sequential"/> is true.)</summary>
		MANUAL,
		///<summary>Vehicle uses a Sequential transmission only.</summary>
		SEQUENTIAL,
		///<summary>Vehicle uses a Sequential transmission which can be toggled into Automatic mode.</summary>
		SEMIAUTO,
		///<summary>Vehicle uses an Automatic transmission which can be toggled into Sequential mode.</summary>
		MANUMATIC,
		///<summary>Vehicle uses an Automatic transmission which can be toggled into Sequential mode.  Cosmetic only; mechanically identical to manumatic in game.</summary>
		AUTOMATIC_SPORT,
		///<summary>Vehicle uses an Automatic transmission only.</summary>
		AUTOMATIC,
		///<summary>Vehicle uses an Automatic transmission only.  Cosmetic only; mechanically identical to automatic in game.</summary>
		CONTINUOUSLY_VARIABLE_TRANSMISSION,
		///<summary>Number of different transmission types possible.</summary>
		NUM_TRANSMISSION_TYPES
		}
	
	///<summary>Limits which models for which a feature applies.</summary>
	public enum EnableState {
		///<summary>No models will be allowed, even those in the blacklist.</summary>
		Off = 0,
		///<summary>Only whitelisted models will be allowed, except those in the blacklist (blacklist overrules whitelist).</summary>
		Select = 1,
		///<summary>All models will be allowed, except those in the blacklist.</summary>
		Blacklist = 2,
		///<summary>All models will be allowed, even those in the blacklist.</summary>
		On = 3
		}

	///<summary>Bitmask to specify a single or combination of meta keys (Alt, Ctrl, Shift).</summary>
	public enum KeyboardMeta {
		///<summary>Must not be holding Alt, Ctrl, or Shift.</summary>
		None = 0,
		///<summary>Must be holding Alt.</summary>
		Alt = 1,
		///<summary>Must be holding Ctrl.</summary>
		Ctrl = 2,
		///<summary>Must be holding Shift.</summary>
		Shift = 4
		}
	
	public enum WheelPosition {
		///<summary>Driver's side front wheel.</summary>
		WheelLeftFront = 0,
		///<summary>Passenger side front wheel.</summary>
		WheelRightFront,
		///<summary>Driver's side mid wheel (in 6-wheel vehicles).</summary>
		WheelLeftMid,
		///<summary>Passenger side mid wheel (in 6-wheel vehicles).</summary>
		WheelRightMid,
		///<summary>Driver's side rear wheel.</summary>
		WheelLeftRear,
		///<summary>Passenger side front wheel.</summary>
		WheelRightRear,

		///<summary>Front wheel of a bicycle.</summary>
		BicycleFront = WheelLeftFront,
		///<summary>Rear wheel of a bicycle.</summary>
		BicycleRear = WheelLeftRear,
		
		///<summary>Front wheel of aircraft landing gear.</summary>
		AircraftFront = WheelLeftFront,
		///<summary>Inner wheel of port landing gear.</summary>
		AircraftFirstLeft = WheelLeftMid,
		///<summary>Outer wheel of port landing gear.</summary>
		AircraftLastLeft = WheelLeftRear,
		///<summary>Inner wheel of starboard landing gear.</summary>
		AircraftFirstRight = WheelRightMid,
		///<summary>Outer wheel of starboard landing gear.</summary>
		AircraftLastRight = WheelRightRear,
		
		TrailerFirstLeft = WheelLeftMid,
		TrailerFirstRight = WheelRightMid,
		TrailerMidLeft = 45,
		TrailerMidRight = 47,
		TrailerLastLeft = WheelLeftRear,
		TrailerLastRight = WheelRightRear
		}
	
	///<summary>A series of identifiers for specific buttons on a gamepad. DPad directions can be used as a
	/// bitmask, as well.</summary>
	///<remarks>
	///WARNING: Only the DPad can be used for bitwise operations.  All other buttons should be treated as unique.
	/// Obviously, using an impossible combination for the DPad will make it impossible to activate.  A perfectly
	/// valid modifier is up right, which will only trigger if the player holds DPadUp+DPadRight.  But another
	/// perfectly valid modifier is up down, which will only trigger if the player holds DPadUp+DPadDown --
	/// obviously not actually physically possible on a working gamepad.
	///<para>
	///(Remember your logic: "valid" does not mean "sound", it just means the format is acceptable.  true == false,
	/// i.e. a hypothetical or premise, is perfectly valid but very unsound.  true = false, i.e. a fact, is both
	/// invalid and unsound.  ("If the world will end tomorrow, I will do X.  The world will end tomorrow.
	/// Therefore I will do X." -- is a perfectly constructed argument that is, of course, completely false.)
	/// And you thought premise deconstruction was just for philosophers!  Heck, "arguments" for functions are
	/// called so by math, whose definition in turn draws directly from Greek logic and deductive reasoning.)
	/// </para>
	///<para>
	///(This dose of history and philosophy in the documentation of an enumeration is brought to you in part by...
	/// Snacky S'mores, the creamy fun of s'mores in a delightful cookie crunch!)
	/// </para>
	///</remarks>
	public enum GamePadButton {
		///<summary>No button is pushed.</summary>
		None = 0,
		A = 1,
		B = 2,
		X = 3,
		Y = 4,
		LB = 5,
		RB = 6,
		LT = 7,
		RT = 8,
		LS = 9,
		RS = 10,
		Start = 11,
		DPadUp = 0x10,
		DPadRight = 0x20,
		DPadDown = 0x40,
		DPadLeft = 0x80
		}
	
	public enum DebugLevel {
		NONE = 0,
		CRITICAL,
		VERBOSE,
		ALL
		}
	
	///// EVENT HANDLERS /////
	
	///<summary>Event handler which will fire for each vehicle in the game (usually about once per second, depending on
	/// <see cref="VehiclesToProcessPerFrame"/>). Other modules can register their functions with this event handler to
	/// receive <see cref="ProcessVehicleEventArgs"/> containing the <see cref="Vehicle"/>, its
	/// <see cref="RealismVehicles.VehicleData"/>, and the (estimated) number of game frames since the last time it was
	/// processed.</summary>
	public static event ProcessVehicleHandler OnProcessVehicle;

	///<summary>Event handler which will fire for only the player's vehicle.  As with <see cref="OnProcessVehicle"/>,
	/// other modules can register to receive <see cref="ProcessVehicleEventArgs"/> containing the
	/// <see cref="Vehicle"/> (<see cref="ProcessVehicleEventArgs.veh"/>) and its
	/// <see cref="RealismVehicles.VehicleData"/> (<see cref="ProcessVehicleEventArgs.vdata"/>).</summary>
	public static event ProcessVehicleHandler OnProcessPlayerVehicle;

	///<summary>Event handler which fires when a <see cref="Vehicle"/> is removed from the handler because its matching
	/// entity no longer exists in Grand Theft Auto V's object table (i.e., the game has freed the memory for that
	/// vehicle). WARNING: <see cref="ProcessVehicleEventArgs.vdata"/> will be null and
	/// <see cref="ProcessVehicleEventArgs.veh"/> will be undefined!  You will likely crash your script if you attempt
	/// any manipulation on a vehicle which does not exist.  Only the <see cref="ProcessVehicleEventArgs.veh.Handle"/>
	/// will be guaranteed to be functional.</summary>
	public static event ProcessVehicleHandler OnCleanupVehicle;
	
	///// PROPERTIES /////

	///<summary>Verifies that the player is in control of a vehicle.</summary>
	public static bool IsPlayerDriving {
		get {
			return Game.Player.Character.IsInVehicle() &&
				Entity.Exists(Game.Player.Character.CurrentVehicle) &&
				Game.Player.Character.CurrentVehicle.Driver == Game.Player.Character;
			}
		}
		
	///<summary>Amount of time between polling the list of all vehicles in the world. Once this list is built, only
	/// <see cref="vehicles_per_frame"/> vehicles are checked per frame. Exception: player's vehicle is checked every
	/// frame.</summary>
	///<remarks>After the list of all vehicles is polled, the delay only begins counting again after the list of
	/// vehicles to check has been emptied.</remarks>
	public static float VehicleScanDelay {
		get { return vehicle_scan_delay; }
		set { vehicle_scan_delay = value; }
		}
	
	///<summary>Should all vehicles be tracked for simulation (true), or only the player's (false)?</summary>
	///<remarks>TODO: This should definitely be split into a number of simulation options.</remarks>
	public static bool SimulateAllVehicles {
		get { return track_all_vehicles; }
		set { track_all_vehicles = value; }
		}
	
	///<summary>Number of vehicles to process each frame.  Relevant only when using <see cref="SimulateAllVehicles"/>.
	/// A higher number will make processing much faster, at the expense of a couple frames per second.</summary>
	public static int VehiclesToProcessPerFrame {
		get { return vehicles_per_frame; }
		set { vehicles_per_frame = Math.Max(1, value); }
		}
	
	///<summary>Level of debugging messages to output.  Not very useful unless you're a programmer for the mod.</summary>
	public DebugLevel Debug {
		get { return debug_level; }
		set { debug_level = value; }
		}
	
	///<summary>Should 46EEK572 licence plates be replaced with a random plate when the vehicle is processed for the first time?</summary>
	public static bool FixLicencePlates {
		get { return fix_licence_plates; }
		set { fix_licence_plates = value; }
		}
	
	///<summary>Should odometers be based on wheel speed? If true, they will be thrown off by powersliding; if false,
	/// they will be based on actual displacement.</summary>
	public static bool AreOdometersAccurate {
		get { return accurate_odometers; }
		set { accurate_odometers = value; }
		}
	
	///<summary>Number of kilometres that will be added to the odometer per in-game kilometre passed to reflect
	/// realistic size of Los Santos and San Andreas). Default 20.0.</summary>
	///<remarks>Defa</remarks>
	public static float OdometerScale {
		get { return distance_scale; }
		set { distance_scale = Math.Max(0.0f, value); }
		}
	
	///// FIELDS /////

	internal static bool fix_licence_plates = true;

	internal static bool accurate_odometers = false;
	
	internal static DebugLevel debug_level = DebugLevel.VERBOSE;

	internal static float distance_scale = 20.0f;

	internal static float vehicle_scan_delay = 4.7f;
	///<summary>Time remaining until next scan for new vehicles (after queue has been emptied).</summary>
	private static float _vehicle_scan_timer = 0.0f;

	internal static bool track_all_vehicles = true;

	internal static int vehicles_per_frame = 8;

	private static Dictionary<int, VehicleData> _vehicle_data = new Dictionary<int, VehicleData>();
	private static int _vehicle_index = 0;
	private static List<Vehicle> _vehicle_queue = new List<Vehicle>();

	///<summary>Vehicles that have been processed on this frame. Used to ensure that when there are fewer than <see cref="vehicles_per_frame" /> loaded vehicles, they do not process faster than once per frame.</summary>
	private static List<int> _handled_vehicles = new List<int>();

	private static Dictionary<string, uint> _hash_cache = new Dictionary<string, uint>();

	private static bool _initialised = false;

	private static string _modfolder = @"scripts\RealismVehicles\";
	private static string _logpath = _modfolder + "log.txt";
	private static string _logcopypath = _modfolder + "last.txt";
	
	///// METHODS /////

	public void RealismCoreTick(object sender, EventArgs parameters) {
		if(Game.FPS <= 0f) return;
		if(Game.IsLoading) {
			if(_vehicle_data.Count > 0) _vehicle_data.Clear();
			return;
			}
		if(Game.IsPaused) return;
		
		ProcessPlayerVehicle();

		if(track_all_vehicles) {
			_handled_vehicles.Clear();
			ScanForNewVehiclesToAdd();

			int this_frame = vehicles_per_frame;
			while(--this_frame >= 0) ProcessNextVehicle();
			}
		}
	
	private void ScanForNewVehiclesToAdd() {
		if(_vehicle_queue.Count == 0) {
			_vehicle_scan_timer -= 1 / Game.FPS;
			if(_vehicle_scan_timer > 0) return;
			
			_vehicle_scan_timer += vehicle_scan_delay;
			Vehicle[] all_vehicles = World.GetAllVehicles();
			_vehicle_queue = all_vehicles.ToList(); //replace queue
			}
		else {
			int this_frame = vehicles_per_frame;
			while(--this_frame >= 0 && _vehicle_queue.Count > 0) {
				Vehicle veh = _vehicle_queue.Last<Vehicle>();
				if(veh != null && Vehicle.Exists(veh) && !IsTrackingVehicle(veh.Handle)) AddVehicle(veh);
				_vehicle_queue.RemoveAt(_vehicle_queue.Count - 1); //pop last element
				}
			}
		}
	
	public static void PlayerVehicleDebug(string message, Vehicle veh, DebugLevel debug = DebugLevel.VERBOSE) {
		if(Core.IsPlayerVehicle(veh)) DebugNotify(message, DebugLevel.VERBOSE);
		}
	public static void DebugNotify(string message) {
		DebugNotify(message, DebugLevel.VERBOSE);
		}
	public static void DebugNotify(string message, DebugLevel level) {
		if(debug_level >= level) UI.Notify("~r~RV.n~s~ " + message);
		}
	
	public static bool AddVehicle(Vehicle veh) {
		if(!IsTrackingVehicle(veh.Handle)) {
			Core.DebugNotify("Tracking new " + veh.FriendlyName + " with handle " + veh.Handle.ToString("x8") + ", currently " + _vehicle_data.Count + " in list", Core.DebugLevel.ALL);
			VehicleData vdata = new VehicleData(veh);
			_vehicle_data[veh.Handle] = vdata;
			return true;
			}
		return false;
		}
	
	public static VehicleData VehicleData(Vehicle veh) {
		if(Vehicle.Exists(veh) && !IsTrackingVehicle(veh.Handle)) {
			AddVehicle(veh);
			}
		return _vehicle_data[veh.Handle];
		}
	
	private static void ProcessPlayerVehicle() {
		Vehicle veh = Game.Player.Character.LastVehicle;
		if(Vehicle.Exists(veh)) {
			if(!IsTrackingVehicle(veh.Handle)) AddVehicle(veh);
			VehicleData vdata = _vehicle_data[veh.Handle];
			ProcessVehicle(veh, vdata);
			//UI.ShowSubtitle(vdata.Odometer.ToString("F1"));
			OnProcessPlayerVehicle?.Invoke(veh, new ProcessVehicleEventArgs(veh, vdata));
			}
		}
	
	///<summary>Processes the next vehicle in the queue.</summary>
	private static void ProcessNextVehicle() {
		if(_vehicle_index < _vehicle_data.Count) {
			KeyValuePair<int, VehicleData> data = _vehicle_data.ElementAt(_vehicle_index);
			Vehicle veh = new Vehicle(data.Key);
			VehicleData vdata = data.Value;
			if(!Entity.Exists(veh)) {
				OnCleanupVehicle?.Invoke(veh, new ProcessVehicleEventArgs(veh, vdata));
				_vehicle_data.Remove(data.Key);
				return;
				}
			
			//If there are so few vehicles loaded that we would process other vehicles more than once per frame,
			// prevent multiple firings for the same vehicle.
			if(_handled_vehicles.Contains(data.Key)) {
				return;
				}
			_handled_vehicles.Add(data.Key);
			
			//UI.ShowSubtitle("Processing vehicle list: " + _vehicle_data.Count + " in list, popping " + " #" + _vehicle_index.ToString("d") + " " + veh.FriendlyName);
			_vehicle_index++;

			if(!IsPlayerVehicle(veh)) { //player's vehicle is checked with a separate function call (once per script frame)
				VehicleData temp = data.Value;
				ProcessVehicle(veh, temp, _vehicle_data.Count / vehicles_per_frame);
				}
			}
		else {
			_vehicle_index = 0;
			}
		}
	
	//Other systems are often called from here.
	private static void ProcessVehicle(Vehicle veh, VehicleData vdata, int frames = 1) {
		/*
		Note that the mod, as currently designed, simply estimates "frames since last process" based on the number of
		 loaded vehicles being tracked (since vehicles_per_frame vehicles are processed per frame, logically the number
		 of frames needed to process any one vehicle is equal to the number of vehicles to process divided by the
		 vehicles_per_frame).  This is rather than actually tracking the number of frames since the vehicle was
		 processed.  We're not particularly concerned with unerring precision while processing NPC vehicles and this
		 keeps the processing loop lean and mean, and -- most importantly -- has no perceptible effect on frame rate.
		 (Actually, as written, the vehicles_per_frame could be bumped up as high as 32 or even 64 and the user should
		 experience no more than a couple lost FPS.
		
		When updating the player's vehicle (which occurs every frame), frames is always 1.
		*/
		if(vdata.TemporarilyFireproof) vdata.TemporaryFireproofing(veh, false);

		float odometer_distance = 0f;
		if(accurate_odometers) {
			float dist = (veh.Position - vdata.LastPosition).Length();
			//Catch teleportations (>100 metres per frame is insanely fast)
			if(dist < 100f && veh.IsOnAllWheels) odometer_distance = dist * distance_scale / 1000f;
			}
		else if(Game.FPS > 0) {
			odometer_distance = veh.WheelSpeed * distance_scale / (float)Game.FPS / (float)frames / 1000f;
			}
		vdata.Odometer += odometer_distance;
		vdata.TripOdometer += odometer_distance;

		OnProcessVehicle?.Invoke(veh, new ProcessVehicleEventArgs(veh, vdata, frames));
		vdata.LastEngineHealth = veh.EngineHealth;
		vdata.LastBodyHealth = veh.BodyHealth;
		vdata.LastTankHealth = veh.PetrolTankHealth;
		vdata.LastPosition = veh.Position;
		}
	
	public static bool IsTrackingVehicle(Vehicle veh) {
		return _vehicle_data.ContainsKey(veh.Handle);
		}
	public static bool IsTrackingVehicle(int handle) {
		return _vehicle_data.ContainsKey(handle);
		}
	
	public static void BackupLastLog() {
		}
	public static void ClearLogFile() {
		try {  File.Delete(_modfolder + _logpath); }
		catch {}
		}
	
	///<summary>Writes a log message to the debug output file (opening and closing the stream immediate to ensure immediate flush of buffer).</summary>
	public static void Log(string message) {
		try {
			StringBuilder builder = new StringBuilder(DateTime.Now.ToString());
			builder.Append(": ");
			builder.Append(message);
			using (StreamWriter writer = new StreamWriter(_logpath, true)) {
				writer.WriteLine(builder.ToString());
				writer.Close();
				}
			}
		catch {
			}
		}
	
	///<summary>Calls the native function to generate a random floating point number between 0.0 to 1.0 (open
	/// ended).</summary>
	public static float RandFloat() { return Function.Call<float>(Hash.GET_RANDOM_FLOAT_IN_RANGE, 0.0f, 1.0f); }
	///<summary>Calls the native function to generate a random floating point number from 0.0 to <paramref name="max"/>
	/// (open ended).</summary>
	public static float RandFloat(float max) { return Function.Call<float>(Hash.GET_RANDOM_FLOAT_IN_RANGE, 0.0f, max); }
	///<summary>Calls the native function to generate a random floating point number from <paramref name="min"/> to
	/// <paramref name="max"/> (open ended).</summary>
	public static float RandFloat(float min, float max) { return Function.Call<float>(Hash.GET_RANDOM_FLOAT_IN_RANGE, min, max); }

	///<summary>Calls the native function to generate a random integer from 0 to 100.</summary>
	public static int RandInt() { return Function.Call<int>(Hash.GET_RANDOM_INT_IN_RANGE, 0, 100); }
	///<summary>Calls the native function to generate a random integer from 0 to <paramref name="max"/> (closed
	/// ended).</summary>
	public static int RandInt(int max) { return Function.Call<int>(Hash.GET_RANDOM_INT_IN_RANGE, 0, max); }
	///<summary>Calls the native function to generate a random integer from <paramref name="min"/> to
	/// <paramref name="max"/> (closed ended).</summary>
	public static int RandInt(int min, int max) { return Function.Call<int>(Hash.GET_RANDOM_INT_IN_RANGE, min, max); }

	///<summary>Produces a random percentage from 0.00000 to 100.00000 with five fixed points of decimal precision.</summary>
	public static float RandPercentage() {
		return ((float)RandInt(0, 10000000))/100000f;
		}
	///<summary>Produces a random percent from 0.00000 to 1.00000 with five fixed points of decimal precision.</summary>
	public static float RandPercent() {
		return ((float)RandInt(0, 100000))/100000f;
		}
	///<summary>Returns a randomly selected element from the parameters, where each item has an equal weight.</summary>
	///<remarks>Requires Microsoft.CSharp reference in your project.  (Does anyone but me find the syntax "static
	/// dynamic" to be utterly asinine?)</remarks>
	public static dynamic Pick(params dynamic[] items) {
		return items[RandInt(0, items.Length-1)];
		}
	///<summary>Randomly returns true the specified ratio of the time.  For instance, OneIn(3) returns true one third of the time.</summary>
	public static bool OneIn(int max) {
		return RandInt(0, max-1) == 0;
		}
	///<summary>Randomly returns true the specified ratio of the time.  For instance, OneIn(3) returns true one third of the time.</summary>
	public static bool OneIn(float max) {
		return RandFloat(0, max) < 1;
		}
	
	///<summary>Returns true if the provided KeyEventArgs exactly match the required metakeys (e.g., if the combination is Alt, it will fail for Alt+Shift).</summary>
	public static bool MatchModifiers(KeyboardMeta metakeys, KeyEventArgs parameters) {
		if(metakeys == KeyboardMeta.None && (parameters.Modifiers & (Keys.Alt | Keys.Control | Keys.Shift)) == 0) return true;
		bool alt_success = ((metakeys & KeyboardMeta.Alt) == 0) || (((metakeys & KeyboardMeta.Alt) == KeyboardMeta.Alt) && parameters.Alt);
		bool ctrl_success = ((metakeys & KeyboardMeta.Ctrl) == 0) || (((metakeys & KeyboardMeta.Ctrl) == KeyboardMeta.Ctrl) && parameters.Control);
		bool shift_success = ((metakeys & KeyboardMeta.Shift) == 0) || (((metakeys & KeyboardMeta.Shift) == KeyboardMeta.Shift) && parameters.Shift);
		return alt_success && ctrl_success && shift_success;
		}
	
	public static bool PressingKeyCombination(KeyboardMeta metakeys, Keys key, KeyEventArgs parameters) {
		if(MatchModifiers(metakeys, parameters) && parameters.KeyCode == key) return true;
		return false;
		}

	///<summary>Returns true if the player is pressing the specified control.</summary>
	public static bool ControlHeld(GTA.Control control) {
		return Game.IsControlPressed(0, control);
		}
	
	///<summary>Returns true if the player is pressing the specified control which is also enabled.</summary>
	public static bool EnabledControlHeld(GTA.Control control) {
		return Game.IsControlPressed(0, control) && Game.IsControlEnabled(0, control);
		}
	
	public static int GetIntDecorator(Entity entity, string decorator) {
		return Function.Call<int>(Hash.DECOR_GET_INT, entity, decorator);
		}
	public static void SetIntDecorator(Entity entity, string decorator, int value) {
		Function.Call<bool>(Hash.DECOR_SET_INT, entity, decorator, value);
		}
	
	///<summary>Parses hash keys into integers for internal use. Used by configuration file loader.</summary>
	///<remarks>The configuration file will accept signed or unsigned integers (e.g., -1851794620, 3999278268),
	/// hexadecimal (0x17D66A12), or the modelname (SULTANRS).  Separate with commas or semicolons.  Whitespace is
	/// ignored.  Unrecognised hashes will be ignored with a log message.</remarks>
	public static ulong ParseVehicleModelHash(string input) {
		bool valid = true;
		uint integer = 0xFFFFFFFF;
		try { integer = System.Convert.ToUInt32(input); }
		catch (Exception E) {
			if(E is OverflowException) valid = false; //too big to fit in a uint32, cannot be valid
			else if(E is FormatException) valid = false; //not an integer-formatted string
			else throw E;
			}
		if(valid && integer != 0) return integer;
		return (ulong)Game.GenerateHash(input);
		}
	
	public static uint CacheHash(string input) {
		string upper = input.ToUpperInvariant();
		if(!_hash_cache.ContainsKey(input)) _hash_cache.Add(upper, (uint)Game.GenerateHash(upper));
		return _hash_cache[input];
		}
	
	public static string HumanReadableHash(int handle) {
		return handle.ToString("x8");
		}
	
	public static string TransmissionGearboxString(TransmissionType tranny) {
		switch(tranny) {
			case (TransmissionType.NONE): { return "none"; }
			case (TransmissionType.MANUAL): { return "manual gearbox"; }
			case (TransmissionType.SEQUENTIAL): { return "sequential gearbox"; }
			case (TransmissionType.SEMIAUTO): { return "semi-automatic gearbox"; }
			case (TransmissionType.MANUMATIC): { return "manumatic gearbox"; }
			case (TransmissionType.AUTOMATIC_SPORT): { return "automatic gearbox with sport shift"; }
			case (TransmissionType.AUTOMATIC): { return "automatic gearbox"; }
			}
		return "unknown";
		}
	
	public static string VehicleClassString(VehicleClass vclass) {
		switch(vclass) {
			case(VehicleClass.Compacts): { return "compact"; }
			case(VehicleClass.Sedans): { return "sedan"; }
			case(VehicleClass.SUVs): { return "SUV"; }
			case(VehicleClass.Coupes): { return "coupe"; }
			case(VehicleClass.Muscle): { return "musclecar"; }
			case(VehicleClass.SportsClassics): { return "classic sportscar"; }
			case(VehicleClass.Sports): { return "sportscar"; }
			case(VehicleClass.Super): { return "supercar"; }
			case(VehicleClass.Motorcycles): { return "motorcycle"; }
			case(VehicleClass.OffRoad): { return "offroader"; }
			case(VehicleClass.Industrial): { return "industrial"; }
			case(VehicleClass.Utility): { return "utility"; }
			case(VehicleClass.Vans): { return "van"; }
			case(VehicleClass.Cycles): { return "cycle"; }
			case(VehicleClass.Boats): { return "boat"; }
			case(VehicleClass.Helicopters): { return "helicopter"; }
			case(VehicleClass.Planes): { return "aircraft"; }
			case(VehicleClass.Service): { return "service"; }
			case(VehicleClass.Emergency): { return "emergency"; }
			case(VehicleClass.Military): { return "military"; }
			case(VehicleClass.Commercial): { return "commercial"; }
			case(VehicleClass.Trains): { return "train"; }
			}
		return "unknown";
		}
	
	public int VehicleCost(VehicleHash vhash) {
		switch(vhash) {
			case(VehicleHash.Adder):		return  1000000;
			case(VehicleHash.Airbus):		return   550000;
			case(VehicleHash.Airtug):		return    14000;
			case(VehicleHash.Akula):		return  3704050;
			case(VehicleHash.Akuma):		return     9000;
			case(VehicleHash.Alpha):		return   150000;
			case(VehicleHash.AlphaZ1):		return  2121350;
			case(VehicleHash.Ambulance):	return   150000;
			case(VehicleHash.Annihilator):	return  1825000;
			case(VehicleHash.APC):			return  3092250;
			case(VehicleHash.Ardent):		return  1150000;
			case(VehicleHash.ArmyTanker):	return   100000; //estimated from real (+ Fuel)
			case(VehicleHash.ArmyTrailer):	return    20000; //estimated from real
			case(VehicleHash.ArmyTrailer2):	return 45020000; //base trailer + Cutter
			case(VehicleHash.Asea):			return    12000;
			case(VehicleHash.Asea2):		return    12000;
			case(VehicleHash.Asterope):		return    26000;
			case(VehicleHash.Autarch):		return    35000;
			case(VehicleHash.Avarus):		return   116000;
			case(VehicleHash.Avenger):		return    45000;
			case(VehicleHash.Avenger2):		return    45000;
			case(VehicleHash.Bagger):		return    16000;
			case(VehicleHash.BaleTrailer):	return     6000; //estimated from real
			case(VehicleHash.Baller):		return    90000;
			case(VehicleHash.Baller2):		return    90000;
			case(VehicleHash.Baller3):		return   149000;
			case(VehicleHash.Baller4):		return   247000;
			case(VehicleHash.Baller5):		return   374000;
			case(VehicleHash.Baller6):		return   513000;
			case(VehicleHash.Banshee):		return   105000;
			case(VehicleHash.Banshee2):		return   565000;
			case(VehicleHash.Barracks):		return   450000;
			case(VehicleHash.Barracks2):	return   400000; //estimated from real
			case(VehicleHash.Barracks3):	return   450000;
			case(VehicleHash.Barrage):		return   150000;
			case(VehicleHash.Bati):			return    10000;
			case(VehicleHash.Bati2):		return    15000;
			case(VehicleHash.Benson):		return    16000; //estimated from real
			case(VehicleHash.Besra):		return   658000;
			case(VehicleHash.BestiaGTS):	return   610000;
			case(VehicleHash.BF400):		return    95000;
			case(VehicleHash.BfInjection):	return    16000;
			case(VehicleHash.Biff):			return   120000; //estimated from real
			case(VehicleHash.Bifta):		return    75000;
			case(VehicleHash.Bison):		return    30000;
			case(VehicleHash.Bison2):		return    30000;
			case(VehicleHash.Bison3):		return    30000;
			case(VehicleHash.BJXL):			return    33750; //extrapolated from GTAO sell
			case(VehicleHash.Blade):		return    15200;
			case(VehicleHash.Blazer):		return     8000;
			case(VehicleHash.Blazer2):		return     8000;
			case(VehicleHash.Blazer3):		return    69000;
			case(VehicleHash.Blazer4):		return    81000;
			case(VehicleHash.Blazer5):		return  1755600;
			case(VehicleHash.Blimp):		return 12000000; //just $12M, and you can order one from a valley girl on your phone anytime you want without supervision! so immersive!
			case(VehicleHash.Blimp2):		return 12000000;
			case(VehicleHash.Blista):		return    28000; //using Blista Compact price for modern variant
			case(VehicleHash.Blista2):		return    16000; //Blista Compact, using extrapolation of GTAO sell of modern Blista for Blista Compact variant
			case(VehicleHash.Blista3):		return    42000; //Go-Go Monkey, using GTAO for custom variant
			case(VehicleHash.Bmx):			return      500;
			case(VehicleHash.BoatTrailer):	return     1500; //estimated from real
			case(VehicleHash.BobcatXL):		return    46000; //extrapolated from GTAO sell
			case(VehicleHash.Bodhi2):		return    24000; //canon GTAO less $1K rust
			case(VehicleHash.Bombushka):	return  5918500;
			case(VehicleHash.Boxville):		return    12000; //20% of canon GTAO
			case(VehicleHash.Boxville2):	return    12000;
			case(VehicleHash.Boxville3):	return    12000;
			case(VehicleHash.Boxville4):	return    12000;
			case(VehicleHash.Boxville5):	return  2926000;
			case(VehicleHash.Brawler):		return   715000;
			case(VehicleHash.Brickade):		return  1110000;
			case(VehicleHash.Brioso):		return    31000; //20% of canon GTAV
			case(VehicleHash.BType):		return   750000;
			case(VehicleHash.BType2):		return   550000;
			case(VehicleHash.BType3):		return   982000;
			case(VehicleHash.Buccaneer):	return    29000;
			case(VehicleHash.Buccaneer2):	return   390000;
			case(VehicleHash.Buffalo):		return    35000;
			case(VehicleHash.Buffalo2):		return    96000; //Buffalo S
			case(VehicleHash.Buffalo3):		return   535000; //Sprunk Buffalo
			case(VehicleHash.Bulldozer):	return   700000; //estimated from real
			case(VehicleHash.Bullet):		return   430000; //500% of canon GTAV
			case(VehicleHash.Burrito):		return    14000; //estimated from real
			case(VehicleHash.Burrito2):		return    14000; //et al.
			case(VehicleHash.Burrito3):		return    14000;
			case(VehicleHash.Burrito4):		return    14000;
			case(VehicleHash.Burrito5):		return    14000;
			case(VehicleHash.Bus):			return   500000;
			case(VehicleHash.Buzzard):		return  2000000; //Armed; using GTAV for armed variant
			case(VehicleHash.Buzzard2):		return  1750000; //Unarmed; using GTAO for unarmed variant
			case(VehicleHash.CableCar):		return    10000; //figure pulled out of my butt, couldn't find a single real source
			case(VehicleHash.Caddy):		return    85000;
			case(VehicleHash.Caddy2):		return    85000;
			case(VehicleHash.Caddy3):		return   120000;
			case(VehicleHash.Camper):		return    20800;
			case(VehicleHash.Caracara):		return  1775000;
			case(VehicleHash.Carbonizzare):	return   195000;
			case(VehicleHash.CarbonRS):		return    40000;
			case(VehicleHash.Cargobob):		return  2200000; //military
			case(VehicleHash.Cargobob2):	return  1995000; //Jetsam
			case(VehicleHash.Cargobob3):	return  1790000; //using GTAO for devalued TPE version
			case(VehicleHash.Cargobob4):	return  2200000; //closed doors
			case(VehicleHash.CargoPlane):	return 80000000;
			case(VehicleHash.Casco):		return   904400;
			case(VehicleHash.Cavalcade):	return    60000;
			case(VehicleHash.Cavalcade2):	return    70000;
			case(VehicleHash.Cheburek):		return    14500;
			case(VehicleHash.Cheetah):		return   650000;
			case(VehicleHash.Cheetah2):		return   865000;
			case(VehicleHash.Chernobog):	return    40000;
			case(VehicleHash.Chimera):		return    21000; //10% of canon GTAO
			case(VehicleHash.Chino):		return    22500; //10% of canon GTAO
			case(VehicleHash.Chino2):		return    40500; //10% canon + 10% canon conversion cost
			case(VehicleHash.Cliffhanger):	return   225000;
			case(VehicleHash.Coach):		return   525000;
			case(VehicleHash.Cog55):		return   154000;
			case(VehicleHash.Cog552):		return   396000;
			case(VehicleHash.CogCabrio):	return   185000;
			case(VehicleHash.Cognoscenti):	return   254000;
			case(VehicleHash.Cognoscenti2):	return   558000;
			case(VehicleHash.Comet2):		return    85000; //Comet
			case(VehicleHash.Comet3):		return    64500; //Comet Retro
			case(VehicleHash.Comet4):		return   710000; //Comet Safari
			case(VehicleHash.Comet5):		return   150000; //Comet SR
			case(VehicleHash.Contender):	return    55000; //20% of canon (canon would only be reasonable for up-armoured)
			case(VehicleHash.Coquette):		return   138000;
			case(VehicleHash.Coquette2):	return   395000;
			case(VehicleHash.Coquette3):	return   695000;
			case(VehicleHash.Cruiser):		return      800; //using GTAO
			case(VehicleHash.Crusader):		return   225000;
			case(VehicleHash.Cuban800):		return   240000;
			case(VehicleHash.Cutter):		return 45000000; //$45M? pfft, I'll take three
			case(VehicleHash.Cyclone):		return   980000; //estimated from real
			case(VehicleHash.Daemon):		return    20000; //using GTAV for base model
			case(VehicleHash.Daemon2):		return   145000; //using GTAO for LS Customs variant
			case(VehicleHash.Defiler):		return    41200; //10% of canon GTAO
			case(VehicleHash.Deluxo):		return    94430; //2% of canon GTAO; presumption it cannot fly
			case(VehicleHash.Diablous):		return   169000;
			case(VehicleHash.Diablous2):	return   169000;
			case(VehicleHash.Dilettante):	return    25000;
			case(VehicleHash.Dilettante2):	return    25000;
			case(VehicleHash.Dinghy):		return   166250; //using pre-Humane Labs price for original variant
			case(VehicleHash.Dinghy2):		return   125000; //using post-Humane Labs price for two-seater variant
			case(VehicleHash.Dinghy3):		return   166250; //heist variant
			case(VehicleHash.Dinghy4):		return   166250; //yacht variant
			case(VehicleHash.DLoader):		return    25000;
			case(VehicleHash.DockTrailer):	return     7500; //estimated from real
			case(VehicleHash.Docktug):		return    75000; //estimated from real
			case(VehicleHash.Dodo):			return   500000;
			case(VehicleHash.Dominator):	return    35000;
			case(VehicleHash.Dominator2):	return   315000; //Pißwasser
			case(VehicleHash.Dominator3):	return    72500; //Dominator GTX; 10% of canon GTAO
			case(VehicleHash.Double):		return    12000; //Double T
			case(VehicleHash.Dubsta):		return    70000; //civil
			case(VehicleHash.Dubsta2):		return    70000; //offroad
			case(VehicleHash.Dubsta3):		return   249000; //6x6
			case(VehicleHash.Dukes):		return    62000;
			case(VehicleHash.Dukes2):		return   279000; //o' death
			case(VehicleHash.Dump):			return  1000000;
			case(VehicleHash.Dune):			return    20000; //Dune Buggy
			case(VehicleHash.Dune2):		return    20000; //Space Docker
			case(VehicleHash.Dune3):		return  1130500; //FAV
			case(VehicleHash.Dune4):		return  3192000; //Ramp Buggy w/ spoiler
			case(VehicleHash.Dune5):		return  3192000; //Ramp Buggy w/o spoiler
			case(VehicleHash.Duster):		return   275000;
			case(VehicleHash.Elegy):		return    90400; //Elegy Retro Custom, 10% of canon
			case(VehicleHash.Elegy2):		return    95000; //Elegy RH8
			case(VehicleHash.Ellie):		return   565000;
			case(VehicleHash.Emperor):		return     8000; //extrapolated from GTAO sell
			case(VehicleHash.Emperor2):		return     5000; //rustbucket
			case(VehicleHash.Emperor3):		return     5000; //snow
			case(VehicleHash.Enduro):		return    48000;
			case(VehicleHash.EntityXF):		return   795000;
			case(VehicleHash.EntityXXR):	return  2305000;
			case(VehicleHash.Esskey):		return    26400; //10% of canon GTAO
			case(VehicleHash.Exemplar):		return   205000;
			case(VehicleHash.F620):			return    80000;
			case(VehicleHash.Faction):		return    36000; //for a well-kept GNC Regal? tolerable but rather high
			case(VehicleHash.Faction2):		return    69500; //Faction + 10% of canon conversion cost
			case(VehicleHash.Faction3):		return   105500; //Faction + 10% of canon conversion cost
			case(VehicleHash.Fagaloa):		return    33500; //10% of canon GTAO
			case(VehicleHash.Faggio):		return     9750; //Faggio Sport, base model $5K + 10% of canon GTAO price
			case(VehicleHash.Faggio2):		return     5000; //Faggio
			case(VehicleHash.Faggio3):		return     5500; //Faggio Mod
			case(VehicleHash.FBI):			return    50000; //$35K Buffalo + $15K police package (estimated from real)
			case(VehicleHash.FBI2):			return    50000; //$35K Granger + $15K police package (estimated from real)
			case(VehicleHash.FCR):			return    13500; //10% of canon GTAO
			case(VehicleHash.FCR2):			return    33100; //13500 + 10% of canon conversion cost
			case(VehicleHash.Felon):		return    95000; //(Felon) using GTAO Felon GT price
			case(VehicleHash.Felon2):		return   100000; //(Felon GT) using GTAV Felon price
			case(VehicleHash.Feltzer2):		return   145000;
			case(VehicleHash.Feltzer3):		return  9750000; //Benefactor Stirling GT -- using 1000% of canon GTAO price
			case(VehicleHash.FireTruck):	return   550000;
			case(VehicleHash.Fixter):		return      500; //estimated from real (fixed-gear custom cruiser bike)
			case(VehicleHash.FlashGT):		return   167500; //10% of canon GTAO
			case(VehicleHash.Flatbed):		return   100000; //estimated from real (flatbed modification of Peterbilt 379)
			case(VehicleHash.FMJ):			return   437500; //25% of canon GTAO
			case(VehicleHash.Forklift):		return    25000; //estimated from real
			case(VehicleHash.FQ2):			return    36000; //estimated from real
			case(VehicleHash.Freight):		return  1500000; //estimated from real
			case(VehicleHash.FreightCar):	return    50000; //estimated from real (heavy flatcar)
			case(VehicleHash.FreightCont1):	return    27000; //estimated from real ($2K ISO + $25K flatcar)
			case(VehicleHash.FreightCont2):	return    27000; //ditto
			case(VehicleHash.FreightGrain):	return   135000; //estimated from real (new boxcar)
			case(VehicleHash.FreightTrailer): return  25000; //estimated from real
			case(VehicleHash.Frogger):		return  1300000;
			case(VehicleHash.Frogger2):		return  1160000; //(TPI version) extrapolated from 89% price valuation of TPI variant of Cargobob
			case(VehicleHash.Fugitive):		return    24000;
			case(VehicleHash.Furoregt):		return   448000;
			case(VehicleHash.Fusilade):		return    36000;
			case(VehicleHash.Futo):			return    11250; //extrapolated from full coverage price/estimated from real (canon handling data of $60K is ridiculous)
			case(VehicleHash.Gargoyle):		return    12000; //10% of canon GTAO
			case(VehicleHash.Gauntlet):		return    32000;
			case(VehicleHash.Gauntlet2):	return   230000; //Redwood Gauntlet
			case(VehicleHash.GB200):		return   940000;
			case(VehicleHash.GBurrito):		return    65000; //using GTAO post-heist cost for Lost Burrito
			case(VehicleHash.GBurrito2):	return    86450; //using GTAO pre-heist cost for A-Team expy
			case(VehicleHash.Glendale):		return    20000; //10% of canon GTAO
			case(VehicleHash.GP1):			return 12600000; //1000% of canon GTAO (an underestimated GTAO price? say what?)
			case(VehicleHash.GrainTrailer): return     3500; //estimated from real
			case(VehicleHash.Granger):		return    35000;
			case(VehicleHash.Gresley):		return    29000;
			case(VehicleHash.GT500):		return  7850000; //1000% of canon GTAO (ANOTHER underestimated GTAO price? now I know they're just messing with us)
			case(VehicleHash.Guardian):		return    75000; //20% of canon GTAO
			case(VehicleHash.Habanero):		return    52500; //extrapolated from full coverage price
			case(VehicleHash.Hakuchou):		return    16400; //20% of canon GTAO
			case(VehicleHash.Hakuchou2):	return    97600; //10% of canon GTAO
			case(VehicleHash.HalfTrack):	return  2254350;
			case(VehicleHash.Handler):		return   200000; //estimated from real
			case(VehicleHash.Hauler):		return    50000; //estimated from real
			case(VehicleHash.Hauler2):		return   140000; //10% of canon GTAO
			case(VehicleHash.Havok):		return   230090; //10% of canon GTAO ($2.3M for a fuckin' ultralight?)
			case(VehicleHash.Hermes):		return    53500; //10% of canon GTAO
			case(VehicleHash.Hexer):		return    15000; //wow, a GTAO Bikers price that's not artificially inflated by 1000%!
			case(VehicleHash.Hotknife):		return    90000;
			case(VehicleHash.HotringSabre):	return   166000; //10% of canon GTAO
			case(VehicleHash.Howard):		return  1296750;
			case(VehicleHash.Hunter):		return  4123000;
			case(VehicleHash.Huntley):		return   195000;
			case(VehicleHash.Hustler):		return   625000;
			case(VehicleHash.Hydra):		return  3990000;
			case(VehicleHash.Infernus):		return   440000;
			case(VehicleHash.Infernus2):	return   915000;
			case(VehicleHash.Ingot):		return     9000;
			case(VehicleHash.Innovation):	return     9250; //10% of canon GTAO
			case(VehicleHash.Insurgent):	return   897750;
			case(VehicleHash.Insurgent2):	return  1350000;
			case(VehicleHash.Insurgent3):	return  1552000; //$1.35M + $202K conversion
			case(VehicleHash.Intruder):		return    16000;
			case(VehicleHash.Issi2):		return    18000;
			case(VehicleHash.Issi3):		return    36000; //10% of canon GTAO
			case(VehicleHash.ItaliGTB):		return  1189000;
			case(VehicleHash.ItaliGTB2):	return  1238500; //$1.189M + 10% of canon conversion 
			case(VehicleHash.Jackal):		return    60000;
			case(VehicleHash.JB700):		return   475000;
			case(VehicleHash.Jester):		return   240000;
			case(VehicleHash.Jester2):		return   350000;
			case(VehicleHash.Jester3):		return    79000; //10% of canon GTAO
			case(VehicleHash.Jet):			return 260000000; //$260M is just walking-around money! right? doesn't everyone own a 737?
			case(VehicleHash.Jetmax):		return   299000;
			case(VehicleHash.Journey):		return    15000;
			case(VehicleHash.Kalahari):		return    40000;
			case(VehicleHash.Kamacho):		return    77000; //no real figure, artistic licence (Land Rover Discovery)
			case(VehicleHash.Khamelion):	return   100000;
			case(VehicleHash.Khanjari):		return  3850350;
			case(VehicleHash.Kuruma):		return    34500; //estimated from real (canon GTAO is ridiculous $126350)
			case(VehicleHash.Kuruma2):		return   104325; //$34.5K + 10% of canon GTAO price ($698250)
			case(VehicleHash.Landstalker):	return    58000;
			case(VehicleHash.Lazer):		return  6500000;
			case(VehicleHash.LE7B):			return  2475000;
			case(VehicleHash.Lectro):		return    19950; //2% of canon... yes, seriously
			case(VehicleHash.Lguard):		return    40000; //Granger + $5K safety package (lightbar et al.)
			case(VehicleHash.Limo2):		return  1650000;
			case(VehicleHash.Lurcher):		return    65000; //10% of canon GTAO
			case(VehicleHash.Luxor):		return  1500000;
			case(VehicleHash.Luxor2):		return 10000000; //Deluxe
			case(VehicleHash.Lynx):			return    86750; //5% of canon GTAO (still overestimates real price by ~$10K)
			case(VehicleHash.Mamba):		return   995000;
			case(VehicleHash.Mammatus):		return   300000;
			case(VehicleHash.Manana):		return    20000; //estimated from real (1975 Buick LeSabre)
			case(VehicleHash.Manchez):		return     6700; //10% of canon GTAO
			case(VehicleHash.Marquis):		return   413990;
			case(VehicleHash.Marshall):		return   250000;
			case(VehicleHash.Massacro):		return   275000;
			case(VehicleHash.Massacro2):	return   385000;
			case(VehicleHash.Maverick):		return   780000;
			case(VehicleHash.Mesa):			return    32000;
			case(VehicleHash.Mesa2):		return    26000;
			case(VehicleHash.Mesa3):		return    87000;
			case(VehicleHash.MetroTrain):	return  1450000;
			case(VehicleHash.Michelli):		return   245000; //20% of canon GTAO
			case(VehicleHash.Microlight):	return    66500; //10% of canon GTAO
			case(VehicleHash.Miljet):		return  1700000;
			case(VehicleHash.Minivan):		return    30000;
			case(VehicleHash.Minivan2):		return    63000; //base $30K + 10% of canon conversion
			case(VehicleHash.Mixer):		return   105000; //estimated from real
			case(VehicleHash.Mixer2):		return   105000;
			case(VehicleHash.Mogul):		return  3125000;
			case(VehicleHash.Molotok):		return  4788000;
			case(VehicleHash.Monroe):		return   490000;
			case(VehicleHash.Monster):		return   556510;
			case(VehicleHash.Moonbeam):		return    32500;
			case(VehicleHash.Moonbeam2):	return    69500; //base $32.5K + 10% of canon conversion
			case(VehicleHash.Mower):		return     1250;
			case(VehicleHash.Mule):			return    27000;
			case(VehicleHash.Mule2):		return    27000;
			case(VehicleHash.Mule3):		return    43225;
			case(VehicleHash.Nemesis):		return    12000;
			case(VehicleHash.Neon):			return  1500000;
			case(VehicleHash.Nero):			return  1440000;
			case(VehicleHash.Nero2):		return  1500500; //base $1.44M + 10% of canon conversiion
			case(VehicleHash.Nightblade):	return    10000; //10% of canon GTAO
			case(VehicleHash.Nightshade):	return   585000;
			case(VehicleHash.NightShark):	return  1245000;
			case(VehicleHash.Nimbus):		return  1900000;
			case(VehicleHash.Ninef):		return   120000;
			case(VehicleHash.Ninef2):		return   130000;
			case(VehicleHash.Nokota):		return  2653350;
			case(VehicleHash.Omnis):		return   701000;
			case(VehicleHash.Oppressor):	return  3524500;
			case(VehicleHash.Oracle):		return    80000;
			case(VehicleHash.Oracle2):		return    82000;
			case(VehicleHash.Osiris):		return  1950000;
			case(VehicleHash.Packer):		return    35000; //estimated from real
			case(VehicleHash.Panto):		return    25000; //estimated from real (canon is $85K)
			case(VehicleHash.Paradise):		return    25000;
			case(VehicleHash.Pariah):		return  1420000;
			case(VehicleHash.Patriot):		return    63000;
			case(VehicleHash.PBus):			return    73150; //10% of canon GTAO
			case(VehicleHash.PCJ):			return     9000;
			case(VehicleHash.Penetrator):	return   880000;
			case(VehicleHash.Penumbra):		return    24000;
			case(VehicleHash.Peyote):		return    29000;
			case(VehicleHash.Pfister811):	return  1135000;
			case(VehicleHash.Phantom):		return   100000;
			case(VehicleHash.Phantom2):		return   255360; //10% of canon GTAO
			case(VehicleHash.Phantom3):		return   122500; //10% of canon GTAO
			case(VehicleHash.Phoenix):		return    13500;
			case(VehicleHash.Picador):		return     9000;
			case(VehicleHash.Pigalle):		return   400000;
			case(VehicleHash.Police):		return    37000; //estimated from real + $15K police package
			case(VehicleHash.Police2):		return    45000; //ditto
			case(VehicleHash.Police3):		return    44000;
			case(VehicleHash.Police4):		return    37000;
			case(VehicleHash.Policeb):		return    14000;
			case(VehicleHash.PoliceOld1):	return    24000; //9K base price (Rancher XL) + $15K police package
			case(VehicleHash.PoliceOld2):	return    25000; //10K base price (Esperanto) + $15K police package
			case(VehicleHash.PoliceT):		return    29000; //14K base price (Burrito) + $15K police package
			case(VehicleHash.Polmav):		return   795000; //base price + $15K police package
			case(VehicleHash.Pony):			return    25000;
			case(VehicleHash.Pony2):		return    25000; //Smoke... on the wa-ter! and fire in the skies! bum ba bum, bum bum ba bum!
			case(VehicleHash.Pounder):		return    28750; //estimated from real
			case(VehicleHash.Prairie):		return    22000; //estimated from real
			case(VehicleHash.Pranger):		return    40000; //base price + $5K life safety package
			case(VehicleHash.Predator):		return   550000; //estimated from real (USCBPM Interceptor)
			case(VehicleHash.Premier):		return    10000;
			case(VehicleHash.Primo):		return     9000;
			case(VehicleHash.Primo2):		return    49000; //base price + 10% of canon conversion
			case(VehicleHash.PropTrailer):	return    65000; //estimated from real
			case(VehicleHash.Prototipo):	return  2700000;
			case(VehicleHash.Pyro):			return  4455500;
			case(VehicleHash.Radi):			return    32000;
			case(VehicleHash.Raiden):		return   137500; //10% of canon GTAO
			case(VehicleHash.RakeTrailer):	return     1550; //estimated from real
			case(VehicleHash.RallyTruck):	return  1300000; //Dune
			case(VehicleHash.RancherXL):	return     9000;
			case(VehicleHash.RancherXL2):	return     9000; //snow
			case(VehicleHash.RapidGT):		return   132000;
			case(VehicleHash.RapidGT2):		return   140000; //cabrio
			case(VehicleHash.RapidGT3):		return   221000; //25% of canon GTAO
			case(VehicleHash.Raptor):		return    64800; //10% of canon GTAO
			case(VehicleHash.RatBike):		return     4800; //10% of canon GTAO
			case(VehicleHash.RatLoader):	return     6000;
			case(VehicleHash.RatLoader2):	return    37500;
			case(VehicleHash.Reaper):		return  1595000;
			case(VehicleHash.Rebel):		return     3000; //rusty
			case(VehicleHash.Rebel2):		return    22000; //clean
			case(VehicleHash.Regina):		return     8000;
			case(VehicleHash.RentalBus):	return    30000;
			case(VehicleHash.Retinue):		return    61500; //10% of canon GTAO
			case(VehicleHash.Revolter):		return   161000; //10% of canon GTAO
			case(VehicleHash.Rhapsody):		return    14000; //10% of canon GTAO
			case(VehicleHash.Rhino):		return  3000000;
			case(VehicleHash.Riata):		return    76000; //20% of canon GTAO
			case(VehicleHash.Riot):			return   500000; //estimated from real
			case(VehicleHash.Riot2):		return   500000;
			case(VehicleHash.Ripley):		return   250000; //estimated from real
			case(VehicleHash.Rocoto):		return    85000;
			case(VehicleHash.Rogue):		return  1596000;
			case(VehicleHash.Romero):		return    27000; //base Washington ($15K) + $12K hearse conversion (estimated from real)
			case(VehicleHash.Rubble):		return   100000; //estimated from real
			case(VehicleHash.Ruffian):		return    10000;
			case(VehicleHash.Ruiner):		return    14000; //estimated from real
			case(VehicleHash.Ruiner2):		return  5745600;
			case(VehicleHash.Ruiner3):		return     3000;
			case(VehicleHash.Rumpo):		return    13000;
			case(VehicleHash.Rumpo2):		return    13000;
			case(VehicleHash.Rumpo3):		return    42250; //base price + 25% of canon GTAO (less base price)
			case(VehicleHash.Ruston):		return    86000; //20% of canon GTAO
			case(VehicleHash.SabreGT):		return    15000;
			case(VehicleHash.SabreGT2):		return    64000; //base price + 10% of canon conversion
			case(VehicleHash.Sadler):		return    35000;
			case(VehicleHash.Sadler2):		return    35000; //snow
			case(VehicleHash.Sanchez):		return     7000; //liveried
			case(VehicleHash.Sanchez2):		return     8000; //plain
			case(VehicleHash.Sanctus):		return   199500; //10% of canon GTAO
			case(VehicleHash.Sandking):		return    45000; //XL
			case(VehicleHash.Sandking2):	return    38000; //SWB
			case(VehicleHash.Savage):		return  2593500;
			case(VehicleHash.Savestra):		return    99000; //10% of canon GTAO
			case(VehicleHash.SC1):			return   160300; //10% of canon GTAO
			case(VehicleHash.Schafter2):	return    65000;
			case(VehicleHash.Schafter3):	return   116000; //V12
			case(VehicleHash.Schafter4):	return    85800; //LWB, base price + 10% of canon GTAO
			case(VehicleHash.Schafter5):	return   148500; //Armoured V12, base price + 10% of canon GTAO
			case(VehicleHash.Schafter6):	return   129600; //Armoured LWB, base price + 10% of canon GTAO
			case(VehicleHash.Schwarzer):	return    80000;
			case(VehicleHash.Scorcher):		return     1000;
			case(VehicleHash.Scrap):		return    40000; //estimated from real
			case(VehicleHash.Seabreeze):	return  1130500;
			case(VehicleHash.Seashark):		return    16899;
			case(VehicleHash.Seashark2):	return    17400; //lifeguard, base price + $500 life safety kit
			case(VehicleHash.Seashark3):	return    16900; //yacht
			case(VehicleHash.Seminole):		return    30000;
			case(VehicleHash.Sentinel):		return    95000; //Sentinel XS
			case(VehicleHash.Sentinel2):	return    60000; //Sentinel
			case(VehicleHash.Sentinel3):	return    65000; //Sentinel Classic, 10% of canon GTAO
			case(VehicleHash.Serrano):		return    50000;
			case(VehicleHash.Seven70):		return   695000;
			case(VehicleHash.Shamal):		return  1150000;
			case(VehicleHash.Sheava):		return   399000; //20% of canon GTAO
			case(VehicleHash.Sheriff):		return    37000; //per POLICE
			case(VehicleHash.Sheriff2):		return    50000; //per FBI2
			case(VehicleHash.Shotaro):		return   222500; //10% of canon GTAO
			case(VehicleHash.Skylift):		return 30000000; //estimated from real; $30M
			case(VehicleHash.SlamVan):		return    49500;
			case(VehicleHash.SlamVan2):		return    49500; //Lost
			case(VehicleHash.SlamVan3):		return    91000; //Custom, base price + 10% of canon conversion
			case(VehicleHash.Sovereign):	return    90000;
			case(VehicleHash.SeaSparrow):	return  1815000;
			case(VehicleHash.Specter):		return   599000;
			case(VehicleHash.Specter2):		return   624200; //base price + 10% of canon conversion
			case(VehicleHash.Speeder):		return   325000;
			case(VehicleHash.Speeder2):		return   325000; //yacht
			case(VehicleHash.Speedo):		return    14000; //estimated from real
			case(VehicleHash.Speedo2):		return    16600; //base price + $2.5K vinyl wrap + $100 accessories
			case(VehicleHash.Squalo):		return   196621;
			case(VehicleHash.Stalion):		return    71000;
			case(VehicleHash.Stalion2):		return   277000; //Burger Shot
			case(VehicleHash.Stanier):		return    22000;
			case(VehicleHash.Starling):		return  3657500;
			case(VehicleHash.Stinger):		return   850000; //using GTAO price for base variant
			case(VehicleHash.StingerGT):	return   875000;
			case(VehicleHash.Stockade):		return    75000; //estimated from real
			case(VehicleHash.Stockade3):	return    75000;
			case(VehicleHash.Stratum):		return    10000;
			case(VehicleHash.Streiter):		return   100000; //20% of canon GTAO
			case(VehicleHash.Stretch):		return    30000;
			case(VehicleHash.Stromberg):	return   318535; //10% of canon GTAO; presumption it cannot be submerged
			case(VehicleHash.Stunt):		return   250000;
			case(VehicleHash.Submersible):	return  1150000; //estimated from real
			case(VehicleHash.Submersible2):	return  1325000;
			case(VehicleHash.Sultan):		return    12000;
			case(VehicleHash.SultanRS):		return    51750; //base price + 5% of canon conversion
			case(VehicleHash.Suntrap):		return    25160;
			case(VehicleHash.Superd):		return   250000;
			case(VehicleHash.Supervolito):	return  2113000;
			case(VehicleHash.Supervolito2):	return  3330000;
			case(VehicleHash.Surano):		return    99000;
			case(VehicleHash.Surfer):		return    11000;
			case(VehicleHash.Surfer2):		return     8000; //rusty
			case(VehicleHash.Surge):		return    38000;
			case(VehicleHash.Swift):		return  1500000;
			case(VehicleHash.Swift2):		return  5150000; //Deluxe
			case(VehicleHash.T20):			return  2200000;
			case(VehicleHash.Taco):			return    50000; //estimated from real
			case(VehicleHash.Tailgater):	return    55000;
			case(VehicleHash.Taipan):		return  1980000;
			case(VehicleHash.Tampa):		return    37500; //10% of canon GTAO
			case(VehicleHash.Tampa2):		return    87250; //Drift, 10% of canon GTAO + 5% of canon conversion
			case(VehicleHash.Tampa3):		return  2108050; //Weaponised
			case(VehicleHash.Tanker):		return   100000;
			case(VehicleHash.Tanker2):		return   100000;
			case(VehicleHash.TankerCar):	return   100000;
			case(VehicleHash.Taxi):			return    23000; //Stanier + $1K taxi equipment
			case(VehicleHash.Technical):	return  1263500;
			case(VehicleHash.Technical2):	return  1489600; //amphib
			case(VehicleHash.Technical3):	return  1405500; //Custom, base + conversion
			case(VehicleHash.Tempesta):		return  1329000;
			case(VehicleHash.Tezeract):		return  2825000;
			case(VehicleHash.Thrust):		return     7500; //10% of canon GTAO
			case(VehicleHash.Thruster):		return  3657500;
			case(VehicleHash.TipTruck):		return    85000; //estimated from real
			case(VehicleHash.TipTruck2):	return    75000;
			case(VehicleHash.Titan):		return  2000000;
			case(VehicleHash.Torero):		return   998000;
			case(VehicleHash.Tornado):		return    30000;
			case(VehicleHash.Tornado2):		return    35000; //convertible
			case(VehicleHash.Tornado3):		return    27000; //beater
			case(VehicleHash.Tornado4):		return    30000; //mariachi
			case(VehicleHash.Tornado5):		return    67500; //Custom, base price + 10% of canon conversion
			case(VehicleHash.Tornado6):		return    37800; //10% of canon GTAO
			case(VehicleHash.Toro):			return  1750000;
			case(VehicleHash.Toro2):		return  1750000;
			case(VehicleHash.Tourbus):		return    30000;
			case(VehicleHash.TowTruck):		return    35000; //large, estimated from real
			case(VehicleHash.TowTruck2):	return    35000; //small, estimated from real
			case(VehicleHash.TR2):			return    30000; //car trailer, estimated from real
			case(VehicleHash.TR3):			return   443990; //boat trailer + $414K Marquis
			case(VehicleHash.TR4):			return 12475000; //car trailer + $10M Z-Type + $1M Stinger + $795K Entity XF + $650K Cheetah
			case(VehicleHash.Tractor):		return     3500; //estimated from real
			case(VehicleHash.Tractor2):		return    80000; //estimated from real
			case(VehicleHash.Tractor3):		return    80000; //snow? estimated from real
			case(VehicleHash.TrailerLarge):	return  1225000; //Mobile Operations Trailer
			case(VehicleHash.TrailerLogs):	return    20000; 
			case(VehicleHash.Trailers):		return    12000; //container or curtain, estimated from real
			case(VehicleHash.Trailers2):	return    18000; //liveried, estimated from real
			case(VehicleHash.Trailers3):	return    12000; //ramp
			case(VehicleHash.Trailers4):	return    12000; //colorable
			case(VehicleHash.TrailerSmall):	return     1400; //utility trailer, estimated from real
			case(VehicleHash.TrailerSmall2): return   10250; //generator trailer, estimated from real
			case(VehicleHash.Trash):		return   120000; //estimated from real
			case(VehicleHash.Trash2):		return   120000;
			case(VehicleHash.TRFlat):		return     8500; //estimated from real
			case(VehicleHash.TriBike):		return     2500; //Whippet
			case(VehicleHash.TriBike2):		return     2500; //Endurex
			case(VehicleHash.TriBike3):		return     2500; //Tri-Cycles
			case(VehicleHash.TrophyTruck):	return   550000;
			case(VehicleHash.TrophyTruck2):	return   695000; //Desert Raid
			case(VehicleHash.Tropic):		return    22000;
			case(VehicleHash.Tropic2):		return    22000; //yacht
			case(VehicleHash.Tropos):		return   816000;
			case(VehicleHash.Tug):			return  1250000;
			case(VehicleHash.Tula):			return  5173700;
			case(VehicleHash.Turismo2):		return   705000;
			case(VehicleHash.Turismor):		return   500000;
			case(VehicleHash.TVTrailer):	return    18000; //Fame or Shame
			case(VehicleHash.Tyrant):		return  1257500; //50% of canon GTAO
			case(VehicleHash.Tyrus):		return  2550000;
			case(VehicleHash.UtilityTruck):	return    65000; //estimated from real
			case(VehicleHash.UtilityTruck2): return   45000;
			case(VehicleHash.UtilityTruck3): return   45000;
			case(VehicleHash.Vacca):		return   240000;
			case(VehicleHash.Vader):		return     9000;
			case(VehicleHash.Vagner):		return  1535000;
			case(VehicleHash.Valkyrie):		return  3790500;
			case(VehicleHash.Valkyrie2):	return  3512500; //estimated from real $3.5M + $12500 M60x2
			case(VehicleHash.Velum):		return   450000;
			case(VehicleHash.Velum2):		return  1323350;
			case(VehicleHash.Verlierer2):	return   695000;
			case(VehicleHash.Vestra):		return   950000;
			case(VehicleHash.Vigero):		return    21000;
			case(VehicleHash.Vigilante):	return  3750000;
			case(VehicleHash.Vindicator):	return    12600; //2% of canon GTAO (!)
			case(VehicleHash.Virgo):		return   195000; //not equivalent to the Virgoes below
			case(VehicleHash.Virgo2):		return    40500; //base price + 10% of canon conversion
			case(VehicleHash.Virgo3):		return    16500; //10% of canon GTAO
			case(VehicleHash.Viseris):		return   175000; //20% of canon GTAO
			case(VehicleHash.Visione):		return  2250000;
			case(VehicleHash.Volatol):		return  3724000;
			case(VehicleHash.Volatus):		return  2295000;
			case(VehicleHash.Voltic):		return   150000;
			case(VehicleHash.Voltic2):		return  3830400;
			case(VehicleHash.Voodoo):		return     5500;
			case(VehicleHash.Voodoo2):		return    47500; //base price + 10% of canon conversion
			case(VehicleHash.Vortex):		return    17800; //5% of canon GTAO
			case(VehicleHash.Warrener):		return    24000; //20% of canon GTAO
			case(VehicleHash.Washington):	return    15000;
			case(VehicleHash.Wastelander):	return    65800; //10% of canon GTAO
			case(VehicleHash.Windsor):		return   845000;
			case(VehicleHash.Windsor2):		return   900000;
			case(VehicleHash.Wolfsbane):	return     9500; //10% of canon GTAO
			case(VehicleHash.XA21):			return  2375000;
			case(VehicleHash.XLS):			return    63250; //25% of canon GTAO
			case(VehicleHash.XLS2):			return   115400; //base price + 10% of canon GTAO
			case(VehicleHash.Yosemite):		return    48500; //10% of canon GTAO
			case(VehicleHash.Youga):		return    16000;
			case(VehicleHash.Youga2):		return    19500; //10% of canon GTAO
			case(VehicleHash.Z190):			return    90000; //10% of canon GTAO
			case(VehicleHash.Zentorno):		return   725000;
			case(VehicleHash.Zion):			return    50000; //using GTA V price for base variant
			case(VehicleHash.Zion2):		return    55000; //base price + $5K GTAO modifier
			case(VehicleHash.ZombieA):		return    19800; //20% of canon GTAO
			case(VehicleHash.ZombieB):		return    24400; //20% of canon GTAO
			case(VehicleHash.ZType):		return 10000000;
			}
		
		uint hash = (uint)vhash;
		//IVPack
		switch(hash) {
			case(0x4B5C5320/*ADMIRAL*/):	return    28000; //GTA IV canon
			case(0xDDF716D8/*ANGEL*/):		return    12000; //estimated from real
			case(0x3AB3B2A8/*BLADE2*/):		return    15200; //GTA:SA canon
			case(0x4020325C/*BOBCAT*/):		return     9800; //estimated from real
			case(0x5E943BFF/*BODHI*/):		return    25000; //clean variant
			case(0xCDC46B21/*BOXVILLE6*/):	return    12000; //20% of canon GTAO
			case(0xA2073353/*BRICKADE2*/):	return   145000; //estimated from real
			case(0x42A4755F/*BUCCANEER3*/):	return    14200; //estimated from real
			case(0xF57463BB/*BUS2*/):		return   400000; //artistic licence: 20% reduction
			case(0x705A3E41/*CABBY*/):		return    34000; //base Minivan + $1K taxi package + $3K wheelchair lift
			case(0xFBFD5B62/*CHAVOS*/):		return     5600; //estimated from real
			case(0x154D122E/*CHAVOS2*/):	return     5600; //Max Payne variant
			case(0x1C18FCE2/*CHEETAH3*/):	return   430000; //Classic; estimated from real
			case(0x3DA97935/*CONTENDER2*/):	return    12800; //SUT; estimated from real
			case(0x98F65A5E/*COQUETTE4*/):	return    16500; //estimated from real
			case(0x09B56631/*DF8*/):		return     4500; //estimated from real
			case(0xE7AD9DF9/*DIABOLUS*/):	return    80000; //estimated from real
			case(0x971AB25B/*DOUBLE2*/):	return    22000; //base price Double T + $10K custom
			case(0xED71E63B/*EMPEROR4*/):	return     5000; //Limo; estimated from real
			case(0xEF7ED55D/*ESPERANTO*/):	return     6300; //estimated from real
			case(0xBE9075F1/*FELTZER*/):	return    20000; //estimated from real
			case(0x3A196CEA/*FEROCI*/):		return     2000; //estimated from real
			case(0x3D285C4A/*FEROCI2*/):	return     4500; //estimated from real + $2.5K vinyl wrap
			case(0x8FC50F21/*FLATBED2*/):	return    25000; //estimated from real
			case(0x98CC6F33/*FLOATER*/):	return    50000; //estimated from real
			case(0x255FC509/*FORTUNE*/):	return     7000; //estimated from real
			case(0x921263C0/*FREEWAY*/):	return     8000;
			case(0xA6297CC8/*FUTO2*/):		return    21000; //estimated from real (Trueno in high demand)
			case(0x28420460/*FXT*/):		return    25000; //estimated from real
			case(0x1CBD35C7/*GHAWAR*/):		return 18000000; //$18M; estimated from real
			case(0xE37C0E92/*HAKUCHOU3*/):	return    82000; //Custom; using canon GTAO (base) price
			case(0xEB9F21D3/*HAKUMAI*/):	return  0;
			case(0x22DC8E7F/*HELLFURY*/):	return  0;
			case(0xEC53AEF1/*HUNTLEY2*/):	return  0;
			case(0x3554E034/*INTERCEPTOR*/): return  0;
			case(0x177DA45C/*JB7002*/):		return  0;
			case(0xFDCAF758/*LOKUS*/):		return  0;
			case(0x2FCECEB7/*LYCAN*/):		return  0;
			case(0xD93DF399/*LYCAN2*/):		return  0;
			case(0x1B25AEB1/*MARBELLE*/):	return  0;
			case(0xB4D8797E/*MERIT*/):		return  0;
			case(0x22C16A2F/*MRTASTY*/):	return  0;
			case(0x4B70D427/*NIGHTBLADE2*/): return  0;
			case(0x08DE2A8B/*NOOSE*/):		return  0;
			case(0x47B9138A/*NRG900*/):		return  0;
			case(0x71EF6313/*NSTOCKADE*/):	return  0;
			case(0x0C5E290F/*PACKER2*/):	return  0;
			case(0x84282613/*PERENNIAL*/):	return  0;
			case(0xA1363020/*PERENNIAL2*/):	return  0;
			case(0xA39F5B77/*PHOENIX2*/):	return  0;
			case(0x07D10BDC/*PINNACLE*/):	return  0;
			case(0x5208A519/*PMP600*/):		return  0;
			case(0xB2FF98F0/*POLICE6*/):	return  0;
			case(0xC4B53C5B/*POLICE7*/):	return  0;
			case(0xD0AF544F/*POLICE8*/):	return  0;
			case(0xEB221FC2/*POLPATRIOT*/):	return  0;
			case(0x10759236/*PREMIER2*/):	return  0;
			case(0x8B0D2BA6/*PRES*/):		return  0;
			case(0xFBCC11F5/*PRES2*/):		return  0;
			case(0x8EB78F5A/*PSTOCKADE*/):	return  0;
			case(0x52DB01E0/*RANCHER*/):	return  0;
			case(0x04F48FC4/*REBLA*/):		return  0;
			case(0x68E27CB6/*REEFER*/):		return  0;
			case(0x9D3D2987/*REGINA2*/):	return  0;
			case(0xAE5ACBC2/*REGINA3*/):	return  0;
			case(0xEA9789D1/*REVENANT*/):	return  0;
			case(0x8CD0264C/*ROM*/):		return  0; //base Esperanto plus $1K taxi package
			case(0xE53C7459/*SABRE*/):		return  0;
			case(0x4B5D021E/*SABRE2*/):		return  0;
			case(0xECC96C3F/*SCHAFTER*/):	return  0;
			case(0x15EF6F16/*SCHAFTERGTR*/): return  0;
			case(0x41D149AA/*SENTINEL3*/):	return  0;
			case(0x38527DEC/*SMUGGLER*/):	return  0;
			case(0x50249008/*SOLAIR*/):		return  0;
			case(0x2A3FE5C1/*SOVEREIGN2*/):	return  0;
			case(0xA9DA270B/*STANIER2*/):	return  0;
			case(0x63FFE6EC/*STEED*/):		return  0;
			case(0x8557F384/*STRATUM2*/):	return  0;
			case(0xA387FB54/*STRETCH2*/):	return  0;
			case(0x94FA5E39/*STRETCH3*/):	return  0;
			case(0x3404691C/*SULTAN2*/):	return  0;
			case(0x61A3B9BA/*SUPERD2*/):	return  0;
			case(0x6C9962A9/*SUPERGT*/):	return  0;
			case(0x480DAF95/*TAXI2*/):		return  0;
			case(0x7C83987C/*TAXI3*/):		return  0;
			case(0x78D70477/*TOURMAV*/):	return  0;
			case(0x8EF34547/*TURISMO*/):	return  1100000; //estimated from real
			case(0x1956C3C8/*TYPHOON*/):	return  0;
			case(0x5B73F5B7/*URANUS*/):		return  0;
			case(0x973141FC/*VIGERO2*/):	return  0;
			case(0xDD3BD501/*VINCENT*/):	return  0;
			case(0x1CDC8CDC/*VIOLATOR*/):	return  0;
			case(0xF0CD0A17/*VOODOO3*/):	return  0;
			case(0xFB5D56B8/*WAYFARER*/):	return  0;
			case(0x737DAEC2/*WILLARD*/):	return  0;
			case(0xFDB50486/*WOLFSBANE2*/):	return  0;
			case(0xBE6FF06A/*YANKEE*/):		return  0;
			case(0x8EDCFA90/*YANKEE2*/):	return  0;
			}
		
		//TODO: Load from user-provided dictionary for any addon models not found here
		return 0;
		}
	
	public static bool IsPlayerVehicle(Vehicle veh) {
		Vehicle pv = Game.Player.Character.CurrentVehicle;
		if(pv != null && pv.Exists() && pv.Handle == veh.Handle) return true;
		return false;
		}

	///<summary>
	///Returns the relative offset of a <paramref name="point"/> from the origin <paramref name="position"/>,
	/// by axis-aligning the point to the <paramref name="rotation"/>, which is assumed to be the heading of the
	/// object at the origin.  For simplicity, only axis-aligns along world Z axis (no XYZ rotation).
	/// </summary>
	public Vector3 GetRelativeOffset(Vector3 point, Vector3 position, float rotation) {
		float sin = (float)Math.Sin(rotation * Math.PI / 180);
		float cos = (float)Math.Cos(rotation * Math.PI / 180);

		float x, y;
		x = (point.X - position.X) * cos - (point.X - position.X) * sin;
		y = (point.Y - position.Y) * sin + (point.Y - position.Y) * cos;
			
		return new Vector3(x, y, point.Z - position.Z);
		}

	///<summary>A non-axis-aligned 2D rectangle, whose vertical normal is the world normal.</summary>
	public struct MapRectangle
	{
		///<summary>Centre point of rectangle (i.e., not upper left).</summary>
		Vector3 origin;

		///<summary>X dimension of rectangle.</summary>
		float width;
		///<summary>Y dimension of rectangle.</summary>
		float height;
		///<summary>Clockwise rotation of rectangle in degrees.</summary>
		float rotation;

		///<summary>Defines a map rectangle in native format.</summary>
		///<param name="loc">Centre point of rectangle (i.e., not upper left).</param>
		///<param name="w">X dimension of rectangle.</param>
		///<param name="h">Y dimension of rectangle.</param>
		///<param name="angle">Clockwise rotation of rectangle in degrees around centre.</param>
		public MapRectangle(Vector3 loc, float w, float h, float angle) {
			origin = loc;
			width = w;
			height = h;
			rotation = angle;
			}

		///<summary>Converts a bounding box to a map rectangle.</summary>
		///<param name="upperleft">Coordinate of upper left of bounding box (first out parameter of <see cref="Model.GetDimensions(out Vector3, out Vector3)"/>).</param>
		///<param name="lowerright">Coordinate of lower left of bounding box (second out parameter of <see cref="Model.GetDimensions(out Vector3, out Vector3)"/>).</param>
		///<param name="angle">Heading of entity, via vector3 ToHeading(), which owns the bounding box.</param>
		public MapRectangle(Vector3 upperleft, Vector3 lowerright, float angle) {
			width = upperleft.X - lowerright.X;
			height = upperleft.Y - lowerright.Y;

			origin.X = upperleft.X + width/2;
			origin.Y = upperleft.Y + height/2;
			origin.Z = upperleft.Z + (lowerright.Z - upperleft.Z)/2;

			rotation = angle;
			}

		///<summary>Provides <paramref name="upperleft"/> and <paramref name="lowerright"/> 'out' parameters from
		/// the map rectangle in the same format as <see cref="Model.GetDimensions(out Vector3, out Vector3)"/>.
		/// </summary>
		///<remarks>Obviously, the Z depth of the resulting bounding box will be zero.</remarks>
		void GetBoundingCoordinates(out Vector3 upperleft, out Vector3 lowerright) {
			float halfwidth = width / 2;
			float halfheight = height / 2;

			//Get dimensions of box
			float lx = origin.X - halfwidth;
			float ly = origin.Y - halfheight;
			float ux = origin.X + halfwidth;
			float uy = origin.Y + halfheight;
				
			upperleft = new Vector3(lx, uy, origin.Z);
			lowerright = new Vector3(ux, ly, origin.Z);
			}

		///<summary>True if the specified <paramref name="point"/> is bounded within the non-axis-aligned
		/// rectangle.</summary>
		public bool PointWithin(Vector3 point) {
			float sin = (float)Math.Sin(rotation * Math.PI/180);
			float cos = (float)Math.Cos(rotation * Math.PI/180);
				
			//Rotate point around origin (add rotation)
			Vector3 rotated_point = AlignPoint(point);
			
			Vector3 upperleft, lowerright;
			GetBoundingCoordinates(out upperleft, out lowerright);
				
			//Rotate rectangle back to world axis (subtract rotation)
			upperleft  = AlignPoint(upperleft, true);
			lowerright = AlignPoint(lowerright, true);

			//Now we have an easy-peasy 2D bound test with an axis-aligned rectangle!
			if(rotated_point.X >= upperleft.X && rotated_point.X <= lowerright.X &&
				rotated_point.Y >= upperleft.Y && rotated_point.Y <= lowerright.Y)
				{
				return true;
				}
			return false;
			}
		
		public bool PointWithin2(Vector3 point) {
			/*
			Based upon PNPOLY algorithm
			Copyright (c) 1970-2003, Wm. Randolph Franklin

			Permission is hereby granted, free of charge, to any person obtaining a copy of this software and
			associated documentation files (the "Software"), to deal in the Software without restriction, including
			without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
			copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the
			following conditions:

			Redistributions of source code must retain the above copyright notice, this list of conditions and the
			following disclaimers.  Redistributions in binary form must reproduce the above copyright notice in the
			documentation and/or other materials provided with the distribution.  The name of W. Randolph Franklin may
			not be used to endorse or promote products derived from this Software without specific prior written
			permission.

			THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT
			LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO
			EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER
			IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE
			USE OR OTHER DEALINGS IN THE SOFTWARE. 
			*/
			//PNPOLY complex polygon bounds test converted to map rectangle system
			//Note that vertices in the PNPOLY algorithm have to be sorted to produce a continuous edge loop.

			Vector3 aligned_point = AlignPoint(point, true);

			Vector3 upperleft, lowerright;
			GetBoundingCoordinates(out upperleft, out lowerright);

			Vector2[] vertices = {
				new Vector2(upperleft.X, upperleft.Y), //Upper Left
				new Vector2(lowerright.X, upperleft.Y), //Upper Right
				new Vector2(lowerright.X, lowerright.Y), //Lower Right
				new Vector2(upperleft.X, lowerright.Y) //Lower Left
				};
			int i, j = 0;
			bool contained = false;
			for(i = 0, j = 3; i < 4; j = i++) {
				if ( ( (vertices[i].Y > aligned_point.Y) != (vertices[j].Y > aligned_point.Y) ) &&
					(aligned_point.X < (vertices[j].X - vertices[i].X) * (aligned_point.Y - vertices[i].Y) /
					(vertices[j].Y - vertices[i].Y) + vertices[i].X) )
					contained = !contained;
				}
			return contained;
			}

		///<summary>Axis-aligns the <paramref name="point"/> to the map rectangle's <see cref="origin"/>,
		/// yielding the point's relative offset from the map rectangle.  Reverse will counteract the
		/// original operation by presuming the angle should be subtracted rather than added.</summary>
		///<remarks>For instance, this can tell you that a point is ahead and to the left of the
		/// rectangle no matter how arbitrarily the rectangle is rotated.  Apply it in reverse to move
		/// it back to its original alignment.</remarks>
		public Vector3 AlignPoint(Vector3 point, bool reverse = false) {
			float sin = (float)Math.Sin(rotation * Math.PI / 180);
			float cos = (float)Math.Cos(rotation * Math.PI / 180);

			float x, y;
			if(!reverse) {
				x = (point.X - origin.X) * cos - (point.X - origin.X) * sin + origin.X;
				y = (point.Y - origin.Y) * sin + (point.Y - origin.Y) * cos + origin.Y;
				}
			else {
				x = (point.X - origin.X) * cos + (point.X - origin.X) * sin + origin.X;
				y = (point.Y - origin.Y) * sin - (point.Y - origin.Y) * cos + origin.Y;
				}

			return new Vector3(x, y, origin.Z);
			}
		}

	private bool OutsideEdge(Vector3 point, Vector3 origin, Vector3 vector) {
		return OutsideEdge(new Vector2(point.X, point.Y), new Vector2(origin.X, origin.Y), new Vector2(origin.X + vector.X, origin.Y + vector.Y));
		}
	private bool OutsideEdge(Vector2 point, Vector2 origin, Vector2 terminus) {
		float x1 = origin.X;
		float y1 = origin.Y;
		float x2 = terminus.X;
		float y2 = terminus.Y;

		float A = -(y2 - y1);
		float B = x2 - x1;
		float C = -(A * x1 + B * y1);

		if(A * point.X + B * point.Y + C >= 0) return true;
		return false;
		}

}

}