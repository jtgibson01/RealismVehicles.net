using GTA;
using GTA.Math;
using GTA.Native;
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using XInputDotNetPure;
using System.Linq;

namespace RealismVehicles
{

public class Engine : Script
{
	public Engine() {
		if(EngineStartKey != Keys.None || EngineStopKey != Keys.None) {
			KeyDown += EngineKeydown;
			}
		if(LeaveEngineRunning) {
			Tick += EngineTick;
			}
		
		Core.OnProcessVehicle += EngineOnProcessVehicle; //register script with Core
		}
	
	public void EngineOnProcessVehicle(object sender, ProcessVehicleEventArgs e) {
		Model model = e.veh.Model;
		if(model.IsBicycle || model.IsHelicopter || model.IsPlane || model.IsTrain) return;
		ProcessEngine(e.veh, Core.VehicleData(e.veh), e.frames);
		}
	
	///// ENGINE MECHANICS /////

	//Engine Start/Stop
	// * Enabled by default.
	// * Press Engine Stop (default Ctrl+Alt+S) to turn off your car's engine.
	// * Press Engine Start (default Ctrl+Alt+W) to turn it on again.
	// * These can be set to the same key without problems, if you want to use the same key to toggle engine state.
	// * Manual start/stop state is cleared automatically upon exiting vehicle; user will not have to start engine
	//    manually unless they have deliberately turned it off without exiting the vehicle.
	// * Manual start/stop can be enforced if desired, but is not recommended.
	// * If engine is already running upon detecting vehicle, it will not be switched off even if manual start/
	//    stop is enabled -- this is for compatibility with story mode.
	// * Note that other mods may compete for control over the engine.  There is nothing that can be done about this.
	
	///<summary>Keyboard <see cref="Keys"/> key to press to turn on vehicle.  Set to <see cref="Keys.None"/> to disable feature.</summary>
	public Keys EngineStartKey {
		get { return engine_start_key; }
		set { engine_start_key = value; }
		}
	///<summary>Keyboard <see cref="Keys"/> key to press to turn off vehicle.  Set to <see cref="Keys.None"/> to disable feature.</summary>
	public Keys EngineStopKey {
		get { return engine_stop_key; }
		set { engine_stop_key = value; }
		}
	
	///<summary>Bitmask of keyboard metakeys to turn off vehicle (0 = None, 1 = Alt, 2 = Ctrl, 4 = Shift).</summary>
	public Core.KeyboardMeta EngineStartMetakeys {
		get { return engine_start_metakeys; }
		set { engine_start_metakeys = value; }
		}
	///<summary>Bitmask of keyboard metakeys to turn off vehicle (0 = None, 1 = Alt, 2 = Ctrl, 4 = Shift).</summary>
	public Core.KeyboardMeta EngineStopMetakeys {
		get { return engine_stop_metakeys; }
		set { engine_stop_metakeys = value; }
		}
	
	//Leave Engine Running
	// * Hold in-game Vehicle Exit control (default F) to exit your current vehicle and shut the engine off.
	// * Tap Vehicle Exit instead to exit your current vehicle without shutting the lights or engine off.
	// * (Tip) Vanilla feature.  Tap Vehicle Exit, hold the forward movement key, and hold Cover (default Q) to take
	//    cover behind your driver's door.
	// * IV Style Exit Vehicle is redundant with this mod, since it's literally the same feature, and may not play
	//    nicely if you have it or some other alternative installed.
	// * If you really prefer using some other mod instead, you should disable this feature here.
	// * Optionally, you will also leave the door open rather than closing it when using the tap.

	///<summary>Whether to enable IV-style vehicle exit. Disable if using another mod that does this too (or
	/// preferably just remove the other mod, since R:V almost certainly does everything that mod does).</summary>
	public bool LeaveEngineRunning {
		get { return enable_leave_engine_running; }
		set { enable_leave_engine_running = value; }
		}

	//Remote Starter (UNIMPLEMENTED)
	// * (Select Models) Press Remote Start (default Ctrl+F) to start or stop your car's engine while within 30 metres.
	// * You will be automatically tasked to enter your vehicle if starting the engine.  Press any movement key to
	//    cancel.

	//Engine Temperature
	// * This simulation applies to wheeled and tracked vehicles only.
	// * The default engine temperature is 0 K above ambient, which is assumed to be 300 K.
	// * Engine temperature properly represents coolant temperature.  The actual temperature of the block internals is
	//    not simulated.  (It's not really necessary as coolant temperature is already a reasonable measure of the
	//    engine's health.)
	// * Temperature flow is measured in kelvin per minute.
	// * The heater will recycle heat until it reaches operating temperature, building up 12.8 K/min until it reaches
	//    64 K.
	// * A running engine will produce heat: 25 K/min at 100% throttle, 1 K/min at 20% throttle (i.e., proportional to
	//    (percentage/20) squared).
	// * By default, NPC vehicles are not tracked.  However, the system is generic and will simulate NPC vehicles with
	//    an easy toggle.
	// * The player's vehicle will build heat based on throttle position.  The throttle is averaged over 60 frames,
	//    rather than computed directly, to reduce the penalty caused by using mouse-and-keyboard.
	// * NPC vehicles do not have throttle position accessible, so their heat gain is based on RPM instead.
	// * TODO: When SHVDN v3 is released, this should be changed to read the throttle position instead of RPM of NPC
	//    vehicles as well.  Currently this is not accessible to SHVDN v2 and memory hacks are only readily available
	//    through C++ mods as SHVDN offers no memory access (all memory access is internal to the assembly).
	// * If velocity along the forward axis is more than 5 cm/s in reverse and the BrakeReverse control input (not
	//    key) is held, we presume that it is throttle.
	// * The current speed of the vehicle on its forward vector will reduce heat, directly proportional to speed in 
	//    m/s, by 0.35 K/min per m/s.  (Note that this means that aggressive powersliding and other manoeuvres will
	//    reduce airflow cooling -- linear speed is safer for your engine than drifting and hillclimb.)
	// * The car's fan will kick in periodically to apply an automatic outflow of 5 K/min.  It will run constantly
	//    when the vehicle is running hot and will shut off when the temperature drops below the target temperature.
	// * If the engine temperature reaches 75, the car's engine health will be reduced gradually over time, by -700 by
	//    the time it reaches 85 (boiling radiator).  As the temperature varies within this range, the engine health
	//    will recover whenever the temperature drops and degrade whenever the temperature rises.
	// * If the engine temperature reaches 85, the car's engine health will be reduced by -200 by the time it reaches
	//    95 (burning oil).
	// * If the engine temperature actually reaches 95, the car's engine begins to break down, never to drive again
	//    (until it respawns or is replaced, if a player vehicle).
	// * Optionally, crossing this failure threshold is an instantaneous destruction of the engine -- otherwise it
	//    simply applies continuous damage as long as the engine continues to run and the temperature remains above
	//    that threshold (regardless of throttle position, unlike the overheat threshold, where the car's engine
	//    condition will vary according to the fuel being burned at any time and can theoretically recover to 100%).
	// * Certain models are more efficient or less efficient at dissipating heat, including multipliers to air cooling
	//    multipliers to heat gain per second, and multipliers to fan effectiveness.  Police vehicles are included in
	//    this list, as are most dedicated racing vehicles.
	// * The engine should play a grinding rattle noise when it throws a rod and breaks down. (UNIMPLEMENTED)
	
	///<summary>Damage applied to engine per second when temperature is below operating threshold if engine is run at max
	/// RPM. Damage is prorated based on RPM over threshold and how cold the engine is.</summary>
	public static float EngineWearAtLowTemperature {
		get { return low_temperature_engine_wear; }
		set { low_temperature_engine_wear = value; }
		}
	///<summary>Maximum RPM percentage (0 to 100) for an engine at low temperature before wear will occur.</summary>
	public static float LowTemperatureRPMLimit {
		get { return low_temperature_max_rpm; }
		set { low_temperature_max_rpm = value; }
		}
	
	///<summary>Engine operating temperature above STP.</summary>
	public static float OperatingTemperature {
		get { return temperature_operating; }
		set { temperature_operating = value; }
		}
	///<summary>Multiplier of operating temperature where the vehicle is considered to have cooled off long enough that
	/// it has no longer reached operating temperature, if it falls below this temperature.</summary>
	public static float ReheatTemperature {
		get { return temperature_reheat; }
		set { temperature_reheat = value; }
		}
	
	///<summary>Engine temperature in K above STP at which the fan will activate.</summary>
	public static float FanActivationTemperature {
		get { return temperature_operating + temperature_fan_activation; }
		set { temperature_fan_activation = value - temperature_operating; }
		}
	public static float FanDeactivationTemperature {
		get { return temperature_operating - temperature_fan_deactivation; }
		set { temperature_fan_deactivation = temperature_operating - value; }
		}
	
	///<summary>Minimum temperature (in degrees Kelvin above 300 K) at which (temporary) engine damage will occur per frame and engine will start steaming.</summary>
	public static float OverheatTemperature {
		get { return temperature_overheat_threshold; }
		set { temperature_overheat_threshold = value; }
		}

	///<summary>Minimum temperature (in degrees Kelvin above 300 K) at which engine will start smoking.</summary>
	public static float BurnOilTemperature {
		get { return temperature_burn_oil_threshold; }
		set { temperature_burn_oil_threshold = value; }
		}
	
	///<summary>Temperature at which the engine will turn itself into a piece of modern art.</summary>
	///<remarks>If <see cref="temperature_engine_failure_instantaneous"/> is true, this will kill the engine.
	/// Otherwise it will suffer <see cref="temperature_engine_failure_damage"/> per second.</remarks>
	public static float EngineFailureTemperature {
		get { return temperature_engine_failure_threshold; }
		set { temperature_engine_failure_threshold = value; }
		}
	
	///<summary>Number of points of damage that will be interpolated between <see cref="OverheatTemperature"/> and
	/// <see cref="BurnOilTemperature"/>.</summary>
	///<example>If OverheatTemperature = 75 and BurnOilTemperature = 85 (both default), and current engine
	/// temperature is 80, then 50% of this value will have been subtracted from the vehicle's health.  If the
	/// temperature drops to 79, 10% of this value will be restored to the engine's health.</example>
	public static float OverheatDamage {
		get { return temperature_overheat_damage; }
		set { temperature_overheat_damage = value; }
		}
	///<summary>Number of points of damage that will be interpolated between <see cref="BurnOilTemperature"/> and
	/// <see cref="EngineFailureTemperature"/>.</summary>
	public static float BurnOilDamage {
		get { return temperature_burn_oil_damage; }
		set { temperature_burn_oil_damage = value; }
		}
	///<summary>Number of points of </summary>
	public static float EngineFailureDamage {
		get { return temperature_engine_failure_damage; }
		set { temperature_engine_failure_damage = value; }
		}
	
	//Environmental Effects of Temperature
	// * Local environments will then affect the engine cooling and heating rate as well as applying a kelvin modifier
	//   to the engine start temperature.
	// * North San Andreas applies a 90% heating multiplier and a 110% cooling multiplier.
	// * Grand Senora Desert and most of central San Andreas applies a 125% heating multiplier and an 80% cooling
	//    multiplier.
	// * North Yankton applies an 80% heating multiplier and a 125% cooling multiplier.
	// * Every 100 m above sea level subtracts 1 K from ambient temperature.
	// * Every 100 m above sea level adds 1% to denominator of heating rate (at 100%, is half as fast) and adds 1%
	//    to numerator of cooling rate (at 100%, is twice as fast).
	// * Vehicles that are 10 m or higher above ground level experience wind chill that adds a bonus 80% heating and
	//    125% cooling.

	//Rapid Cooling
	// * Blast the engine block with a fire extinguisher to lower the engine temperature by 3 K per second.
	// * Immerse the vehicle in water to lower the engine temperature by 5 K per second at full immersion -- being sure not
	//    to flood the engine, of course.

	///<summary>Number of degrees cooled per second of application of a fire extinguisher blast to the engine block.</summary>
	public float FireExtinguisherCooling {
		get { return fire_extinguisher_cooling_rate; }
		set { fire_extinguisher_cooling_rate = value; }
		}
	
	///<summary>Number of degrees cooled per second the vehicle remains immersed in water. Partial immersion is
	/// linearly proportional (e.g., 50% immersion is 50% as fast).</summary>
	public float ImmersionCooling {
		get { return immersion_cooling_rate; }
		set { immersion_cooling_rate = value; }
		}

	//Rough Handling (UNIMPLEMENTED)
	// * Recommended to enable, but left disabled for compatibility with Manual Transmission mod (which also has an
	//    optional RPM-damage system).
	// * (Disabled by default) An engine running at max RPM will suffer continuous wear at a cumulatively increasing
	//    rate of 1 point per second (e.g., -1 point after 1 second, -2 points after 2 seconds (total -3), -3 points
	//    after 3 seconds (total -6), etc.).  The percent rate of wear between 100% RPM and the rough handling threshold is
	//    squared, so that the rate will be minimal but then and exponentially approach 1.0 at max RPM.
	// * (Disabled by default) An engine that has not yet reached operating temperature will suffer 1 point of wear per
	//    second it is over 70% RPM.  This threshold will increase as the vehicle approaches operating temperature
	//    (e.g., if at 50% of operating temperature (STP+32), this threshold will be 70% + (0.5*(100-70)) = 85%).  Wear
	//    will likewise proportionately decrease if the engine is very cold -- e.g., at 200 m above sea level in North
	//    Yankton, which starts off at -25 STP, the engine will receive damage at any level over 30% RPM!

	//Bonus Damage
	// * All engine damage is exponentiated by 1.25 (configurable) whenever it is received.
	// * e.g., 100 pts = 316.  200 pts = 750.  252 pts = 1004 = that's it for the engine.
	// * Thus, love taps produce around the same amount of damage as before, but hard collisions (or direct bullet
	//    impacts to the engine) are considerably more likely to destroy the engine.

	///<summary>Minimum amount of damage since last check before we will exponentiate damage to a vehicle.</summary>
	public static float BonusDamageThreshold {
		get { return bonus_damage_threshold; }
		set { bonus_damage_threshold = value; }
		}
	public static float EngineDamageMultiplier {
		get { return bonus_damage_multiplier; }
		set { bonus_damage_multiplier = value; }
		}
	
	//Anti-Explosion
	// * If the fuel health is less than -500, fuel health is reset to -500.
	// * For all processed vehicles, if engine health is below zero, it is reset to -1.
	// * Under high load situations, with a low <see cref="vehicles_per_frame"/>, this may
	//    not be enough to prevent an explosion.
	
	///<summary>Prevent vehicles from exploding when on fire?</summary>
	public static bool AntiExplosionEnabled {
		get { return anti_explosion; }
		set { anti_explosion = value; }
		}
	
	//Unexplained Fires
	// * The literal opposite of the anti-explosion feature.
	// * If a vehicle is involved in a rollover, it can burst into flames.
	
	///<summary>Should certain whitelisted vehicles explode when they flip upside down?  Fun AND realistic!</summary>
	public static bool UnexplainedFiresEnabled {
		get { return unexplained_fires_enabled; }
		set { unexplained_fires_enabled = value; }
		}
	
	//Precision Immobilisation Technique - P.I.T. Manoeuvre
	// * If the vehicle's wheel speed is the opposite of its current gear, it will suffer damage proportional to its
	//    speed above 18 m/s, divided by 5 m/s, squared, and multiplied by 100.  For instance, if travelling at 28 m/s
	//    by the time it spins 180 degrees, the vehicle will suffer 400 points of engine damage per second.

	//Linked Health (UNIMPLEMENTED)
	// * If enabled, the vehicle's engine has a limit to its condition as a percentage multiplier of the body's damage.
	// * The multiplier is set to 75% by default.
	// * For example, if the body is damaged by 50%, and the multiplier is 0.75, then the engine's maximum condition is
	//    62.5%.
	
	///<summary>Should the engine health be affected by a certain percentage of damage to the car's body?</summary>
	public bool LinkEngineHealthToBodyHealth {
		get { return link_engine_health_to_body; }
		set { link_engine_health_to_body = value; }
		}
	
	///<summary>The percentage of damage to the body that should be reflected in the engine.</summary>
	public float LinkedEngineHealthMultiplier {
		get { return linked_health_multiplier; }
		set { linked_health_multiplier  = value; }
		}
	
	//Random Breakdowns (UNIMPLEMENTED)
	// * Every 600 to 3600 seconds, a single vehicle in the game will receive from 0 to 1250 damage to the engine.
	// * The number of vehicles loaded into memory increases or reduces the frequency at which this calculation
	//    occurs, with a base presumption of 200 vehicles loaded (heavy freeway traffic).  For instance, if there is
	//    only one vehicle loaded (the player's vehicle) and a failure is predicted to occur after 600 seconds, it
	//    will actually occur only once every 120000 seconds (literally, only after 33 realtime hours of gameplay).
	// * This means that the overall probability of any specific car failing is flat regardless of the number of cars
	//    loaded, but the overall probability of any random car failing increases with the numbers of cars loaded --
	//    just like real life.
	// * The blowout time is not saved, so there is a guaranteed minimum window of several minutes after starting the
	//    game if you have a mission-critical mission to play.  This breakdown system is designed to simulate the
	//    likelihood of a chaotic failure (like a timing belt snapping) and not intended to be an actual dimension of
	//    gameplay for police pursuits etc.).
	// * Note that this random blowout does not count for "bonus damage" -- i.e., unless the engine has suffered
	//    significant damage, it will often survive the blowout.
	
	//Engine Start/Stop
	internal Keys engine_start_key = Keys.W;
	internal Keys engine_stop_key = Keys.S;
	internal Core.KeyboardMeta engine_start_metakeys = Core.KeyboardMeta.Alt | Core.KeyboardMeta.Ctrl;
	internal Core.KeyboardMeta engine_stop_metakeys = Core.KeyboardMeta.Alt | Core.KeyboardMeta.Ctrl;

	//Leave Engine Running
	internal bool enable_leave_engine_running = true;
	private float _key_off_held_time = 0.0f;
	private bool _engine_was_on = false;
	private bool _exiting_vehicle = false;

	//Temperature
	internal static float low_temperature_engine_wear = 1.0f;
	internal static float low_temperature_max_rpm = 70f;
	internal static float low_temperature_heating = 12.8f;

	internal static float temperature_operating = 64f;
	internal static float temperature_reheat = 0.8f;
	
	internal static float temperature_overheat_threshold = 75f;
	internal static float temperature_overheat_rate = 1.0f;
	internal static float temperature_overheat_damage = 650f;
	
	internal static float temperature_burn_oil_threshold = 85f;
	internal static float temperature_burn_oil_damage = 150f;
	internal static float temperature_engine_failure_threshold = 95f;
	
	///<summary>Should engines break down immediately upon reaching <see cref="EngineFailureTemperature"/>?</summary>
	internal static bool temperature_engine_failure_instantaneous = false;
	///<summary>If <see cref="temperature_engine_failure_instantaneous"/> is false, this is the amount of damage per
	/// second to the engine while over the <see cref="EngineFailureTemperature"/> incurred as long as the engine is
	/// running.</summary>
	internal static float temperature_engine_failure_damage = 2.0f;

	///<summary>Amount of temperature gained per second at redline (K/min).</summary>
	internal static float temperature_gain_throttle_redline = 25f;
	///<summary>Amount of temperature gained per second at idle (K/min).</summary>
	internal static float temperature_gain_throttle_idle = 1f;

	///<summary>Multipliers (added to 1.0) of active cooling of engine proportional to vehicle engine upgrades (level 1
	/// through level 4).</summary>
	///<remarks>There are some mods that include additional levels, so this list is more than big enough to accommodate
	/// them, but you will rarely see anything above temperature_engine_upgrade_bonus[3] (i.e., the 4th
	/// index).</remarks>
	internal static float[] temperature_engine_upgrade_bonus = { 0.15f, 0.25f, 0.35f, 0.45f, 0.55f, 0.65f, 0.75f, 0.85f, 0.95f, 1.0f };

	///<summary>Temperature in kelvin dissipated per minute per metre-per-second of velocity directly forward.</summary>
	internal static float temperature_loss_air_cooling = 0.35f;
	
	///<summary>Temperature in kelvin dissipated per (real) minute while the fan is running.</summary>
	internal static float temperature_loss_fan = 5f;
	
	///<summary>Multipliers to active cooling in select models.</summary>
	internal static Dictionary<VehicleHash, float> temperature_loss_multipliers = new Dictionary<VehicleHash, float>() {
		//Racing
		{ VehicleHash.Blista3, 1.25f }, { VehicleHash.Buffalo3, 1.25f }, { VehicleHash.Dominator2, 1.25f },
		{ VehicleHash.Jester2, 1.25f }, { VehicleHash.Massacro2, 1.25f }, { VehicleHash.Omnis, 1.25f },
		{ VehicleHash.RallyTruck, 1.25f }, { VehicleHash.Stalion2, 1.25f }, { VehicleHash.TrophyTruck, 1.25f },
		{ VehicleHash.TrophyTruck2, 1.35f },
		//Emergency
		{ VehicleHash.Police, 1.2f }, { VehicleHash.Police2, 1.2f }, { VehicleHash.Police3, 1.25f },
		{ VehicleHash.Pranger, 1.2f }, { VehicleHash.PoliceT, 1.2f }, { VehicleHash.FBI, 1.2f },
		{ VehicleHash.FBI2, 1.2f }, { VehicleHash.Sheriff, 1.2f }, { VehicleHash.Sheriff2, 1.2f },
		//Heavy Duty
		{ VehicleHash.Benson, 1.2f }, { VehicleHash.Flatbed, 1.2f }, { VehicleHash.Hauler, 1.2f },
		{ VehicleHash.Hauler2, 1.2f }, { VehicleHash.Packer, 1.2f }, { VehicleHash.Phantom, 1.2f },
		{ VehicleHash.Phantom2, 1.2f }, { VehicleHash.Phantom3, 1.2f }, { VehicleHash.Pounder, 1.2f },
		{ VehicleHash.Stockade, 1.2f }, { VehicleHash.Stockade3, 1.2f }, { VehicleHash.Mesa3, 1.2f },
		
		///// IVPack /////
		//Emergency
		{ (VehicleHash)0x08DE2A8B/*NOOSE*/, 1.2f }, 

		//World of Variety
		{ (VehicleHash)0xF0DFD0A3/*SHERIFF3*/, 1.2f }
		//Dispatch of Variety
		//VanillaWorks
		//DispatchWorks
		};
	
	///<summary>Temperature in kelvin dissipated per minute while engine is off.</summary>
	internal static float temperature_loss_off = 6.15f;
	
	///<summary>Temperature in kelvin above operating temperature at which cooling fan engages.</summary>
	internal static float temperature_fan_activation = 0.5f;
	///<summary>Temperature in kelvin below operating temperature at which cooling fan cuts off.</summary>
	internal static float temperature_fan_deactivation = 0.1f;

	///<summary>Whether the player's vehicle should use average throttle position rather than RPM to determine heat gained.</summary>
	internal static bool use_throttle_average = true;

	///<summary>List of the last sixty throttle inputs of the player's vehicle for averaging when <see cref="use_throttle_average"/> is true.</summary>
	private static float[] _player_throttle_history = new float[60];
	///<summary>Current zero-based index (out of 60) being checked in the <see cref="_player_throttle_history"/>.</summary>
	private static int _last_throttle_index;
	
	///<summary>Although generally unimportant for the simulation, the base temperature is assumed to be 300 K (25 ºC or 80 ºF).</summary>
	private const float STP_TEMPERATURE = 300f;
	///<summary>No matter the unusual circumstances, environmental temperature can never be below 225 K (-50 ºC or -50 ºF).</summary>
	private const float MINIMUM_AMBIENT_TEMPERATURE = 225f;
	///<summary>No matter the unusual circumstances, environmental temperature can never be above 325 K (50 ºC or 125 ºF).</summary>
	///<remarks>"But fires!" Ambient temperature is used in this mod for the average temperature of cool, stopped
	/// vehicle, not for actual atmospheric temperature. ;-)</remarks>
	private const float MAXIMUM_AMBIENT_TEMPERATURE = 325f;

	//Rapid Cooling
	internal static float fire_extinguisher_cooling_rate = 3.0f;
	internal static float immersion_cooling_rate = 5.0f;

	//Rough Handling
	internal static float rough_handling_threshold = 99.0f;
	internal static float rough_handling_damage_rate = 10.0f;

	//Bonus Damage
	internal static float bonus_damage_multiplier = 1.0f;
	internal static bool bonus_damage_enabled = true;
	internal static float bonus_damage_threshold = 5f;
	internal static float bonus_damage_exponent = 1.25f;
	internal static float bonus_damage_random_min = 0.5f;
	internal static float bonus_damage_random_max = 1.5f;
	
	///<summary>Vehicles which have reinforcements, bullbars, or armour, and are therefore immune to bonus engine damage.</summary>
	internal static List<VehicleHash> bonus_damage_immune_models = new List<VehicleHash>() {
		VehicleHash.APC, VehicleHash.Bulldozer, VehicleHash.Baller5, VehicleHash.Benson, VehicleHash.BobcatXL, VehicleHash.Bodhi2, VehicleHash.Boxville5,
		VehicleHash.Barracks, VehicleHash.Barracks2, VehicleHash.Barracks3, VehicleHash.Barrage, VehicleHash.Chernobog, VehicleHash.GBurrito, VehicleHash.GBurrito2,
		VehicleHash.Cog552, VehicleHash.Cognoscenti2, VehicleHash.Contender, VehicleHash.Bulldozer, VehicleHash.Dukes2, VehicleHash.Dump, VehicleHash.Freight,
		VehicleHash.HalfTrack, VehicleHash.Hauler, VehicleHash.Hauler2, VehicleHash.Insurgent, VehicleHash.Insurgent2, VehicleHash.Insurgent3,
		VehicleHash.Kamacho, VehicleHash.Khanjari, VehicleHash.Kuruma2, VehicleHash.Mesa3, VehicleHash.Mule3, VehicleHash.NightShark,
		VehicleHash.Police, VehicleHash.Police2, VehicleHash.Police3, VehicleHash.Police4, VehicleHash.PoliceT,
		VehicleHash.Pranger, VehicleHash.RallyTruck, VehicleHash.Riot, VehicleHash.Riot2, VehicleHash.Rhino, VehicleHash.Rumpo3, VehicleHash.Schafter6,
		VehicleHash.Sheriff, VehicleHash.Sheriff2, VehicleHash.Technical, VehicleHash.Technical2, VehicleHash.TowTruck2,
		VehicleHash.TrophyTruck, VehicleHash.UtilityTruck3, VehicleHash.XLS2,
		//IVPack
		(VehicleHash)0x08DE2A8B/*NOOSE*/, (VehicleHash)0x71EF6313/*NSTOCKADE*/,
		//WOV
		(VehicleHash)0xF0DFD0A3/*SHERIFF3*/
		//DOV
		//VanillaWorks
		//DispatchWorks
		};

	//Anti-Explosion
	internal static bool anti_explosion = true;

	//Unexplained fires
	internal static bool unexplained_fires_enabled = false;
	internal static List<VehicleHash> unexplained_fire_models = new List<VehicleHash>() {
		VehicleHash.Blista, VehicleHash.Voltic, VehicleHash.Voltic2, VehicleHash.Panto
		};
	
	//Linked health
	internal static bool link_engine_health_to_body = false;
	internal static float linked_health_multiplier = 0.75f;

	//Handles the IV-style engine shutoff.
	//ProcessEngine() is event driven and called from the Core's vehicle processor instead -- see OnProcessVehicle.
	private void EngineTick(object sender, EventArgs parameters) {
		if(!enable_leave_engine_running) {
			Tick -= EngineTick;
			return;
			}
		
		if(!Core.IsPlayerDriving) {
			_key_off_held_time = 0.0f;
			_engine_was_on = false;
			_exiting_vehicle = false;
			}
		else {
			/*
			This logic is a bit thick and hard to follow, so here goes my best attempt at documenting:
			* First, the game checks to see if the player is in the exiting task.  If so, the mod is flagged that the
			   player is exiting.
			* If the player has been holding down the exit key, the engine will shut off after one second of holding
			   the key.
			* Otherwise, if the mod detects that the vehicle's engine has been shut off (due to hard-coded game
			   behaviour), it will be automatically turned back on (every frame, if necessary).
			* If the player is not performing the exiting task, then it checks to see if the flag is not set, to ensure
			   that the player didn't just finish the task.  (This safety check is probably not needed as the task
			   probably only completes after the game has already confirmed the player is no longer present inside the
			   vehicle, but it's more robust and safe to check anyway and it's not like a single boolean check will
			   impact the frame rate.)
			* If the exiting flag is not set, the game will remember whether the engine was running or not every frame.
			   This prevents the vehicle's engine from being automatically restarted while exiting, once the exiting
			   task initiates, if the engine was already off.  Conversely, once the player is exiting, the mod will
			   treat the engine status as "write only" and no longer check or remember its current running state (as
			   both the game and the mod will be fighting over the engine state anyway (except, this mod will win -- if
			   you're going to fight, fight to win ;-)).
			* Once the player is no longer driving any vehicle (start of 'if' above), i.e., has exited the vehicle, all
			   the flags are reset.
			*/
			if(Core.EnabledControlHeld(GTA.Control.VehicleExit)) {
				_key_off_held_time += (1 / Math.Max(1, Game.FPS));
				}
			const int EXITING_VEHICLE_OPENING_DOOR_EXITING = 167;
			Vehicle veh = Game.Player.Character.CurrentVehicle;
			if(Function.Call<bool>(Hash.GET_IS_TASK_ACTIVE, Game.Player.Character, EXITING_VEHICLE_OPENING_DOOR_EXITING)) {
				_exiting_vehicle = true;
				if(_key_off_held_time > 1.0f) veh.EngineRunning = false;
				else if(_engine_was_on && !veh.EngineRunning) veh.EngineRunning = true;
				}
			else {
				if(!_exiting_vehicle) {
					if(veh.EngineRunning) _engine_was_on = true;
					else _engine_was_on = false;
					}
				}
			}
		}
	
	//Handles the engine start/stop feature.  Will automatically disable itself if the keys are disabled
	private void EngineKeydown(object sender, KeyEventArgs parameters) {
		if(EngineStartKey == Keys.None && EngineStopKey == Keys.None) {
			KeyDown -= EngineKeydown;
			return;
			}
		if(Core.IsPlayerDriving) {
			Vehicle veh = Game.Player.Character.CurrentVehicle;
			VehicleData vdata = Core.VehicleData(veh);

			if(parameters.KeyCode == engine_start_key) {
				if(Core.MatchModifiers(engine_start_metakeys, parameters)) EngineOn(veh, vdata);
				}
			if(parameters.KeyCode == engine_stop_key) {
				if(Core.MatchModifiers(engine_stop_metakeys, parameters)) EngineOff(veh, vdata);
				}
			}
		}
	
	private void EngineOff(Vehicle veh, VehicleData data) {
		if(veh.EngineRunning) {
			data.KeyOnOff(veh, false);
			UI.Notify("~g~Ignition off.~s~");
			return;
			}
		}
	private void EngineOn(Vehicle veh, VehicleData data) {
		if(!veh.EngineRunning) {
			data.KeyOnOff(veh, true);
			UI.Notify("~g~Ignition on.~s~");
			return;
			}
		}
	
	//Called from OnProcessVehicle
	///<summary>Handle all of the engine-related features of the vehicle.</summary>
	public static void ProcessEngine(Vehicle veh, VehicleData vdata, int frames_since_last_process = 1) {
		float fps_multiplier = frames_since_last_process / (Math.Max(1, Game.FPS));
		
		float damage = (vdata.LastEngineHealth - veh.EngineHealth);
		if(damage != 0) Core.DebugNotify("Damage last frame for " + veh.FriendlyName + ": " + damage.ToString("F2"), Core.DebugLevel.ALL);
		//React to being repaired
		if(damage < 0 || vdata.LastBodyHealth < veh.BodyHealth || vdata.LastTankHealth < veh.PetrolTankHealth) {
			Core.PlayerVehicleDebug("Repair amount = " + (vdata.LastEngineHealth-veh.EngineHealth).ToString("F2"), veh, Core.DebugLevel.ALL);
			HandleRepair(veh, vdata);
			damage = 0f;
			Core.PlayerVehicleDebug(veh.FriendlyName + " repaired.", veh);
			}
		
		//Bonus damage
		if(bonus_damage_enabled && damage > 0 && damage > bonus_damage_threshold && veh.EngineHealth > 0) {
			Core.DebugNotify("Health " + veh.EngineHealth + ", Last " + vdata.LastEngineHealth + " = " + Convert.ToInt32(damage).ToString("d"), Core.DebugLevel.ALL);
			ApplyBonusDamage(veh, vdata, damage);
			}
		
		//Anti-explosion (repair engine to let it burn forever*) * May not be forever. Warranty void if car is on fire.
		HandleAntiExplosion(veh, vdata);

		//Unexplained fires are a matter for the courts! Canyoneeeeeroooooo
		UnexplainedFire(veh, fps_multiplier);
		
		//Blasted with fire extinguisher (player only)
		Ped pc = Game.Player.Character;
		if(pc.IsOnFoot && pc.IsShooting && pc.Weapons.Current.Hash == WeaponHash.FireExtinguisher) {
			CoolWithFireExtinguisher(veh, vdata, fps_multiplier);
			}
		
		//Submerged
		ApplyImmersionCooling(veh, vdata, fps_multiplier);
		
		//Engine temperature
		if(veh.EngineRunning) {
			float gain_rate = temperature_gain_throttle_redline - temperature_gain_throttle_idle;

			float gained_temperature = 0f;
			
			float forward_velocity = GTA.Math.Vector3.Dot(veh.ForwardVector, veh.Velocity);
			
			int throttle_count = _player_throttle_history.Count();
			
			//Use average throttle for player's vehicle's temperature gain, if enabled.
			if(veh.Driver == Game.Player.Character && use_throttle_average) {
				float throttle_position = Game.GetControlNormal(0, GTA.Control.VehicleAccelerate);

				//Presume that travelling in reverse > 5 cm/sec with the "Brake"/Reverse control held means throttle
				if(forward_velocity < -0.05f) {
					throttle_position = Game.GetControlNormal(0, GTA.Control.VehicleBrake);
					}
				
				int index = _last_throttle_index++;
				if(_last_throttle_index >= throttle_count) _last_throttle_index = 0;

				_player_throttle_history[index] = throttle_position;

				float throttle_total = 0;
				for(int i = 0; i < throttle_count; i++) throttle_total += _player_throttle_history[i];
				throttle_total /= throttle_count;

				gained_temperature += (throttle_total * throttle_total * gain_rate + temperature_gain_throttle_idle);
				}
				
			//Otherwise, use engine RPM
			else {
				//The magic numbers below are calculated from the game's hard-coded idle RPM of 20% (where 20%*20% = 0.04, and 1-(20%*20%) = 0.96)
				float rpm_mult = Math.Max( Math.Min((veh.CurrentRPM * veh.CurrentRPM - 0.04f) / 0.96f, 1f), 0f );
				gained_temperature += (rpm_mult * gain_rate * EnvironmentHeatingMult(veh) + temperature_gain_throttle_idle);
				}
				
			//Active heating (for simplicity instead of a complex thermodynamic equation)
			if(vdata.EngineTemperature < OperatingTemperature && !vdata.IsFanRunning) {
				gained_temperature += low_temperature_heating * EnvironmentHeatingMult(veh);
				}
			
			if(!vdata.OperatingTemperatureReached && vdata.EngineTemperature >= OperatingTemperature) {
				vdata.OperatingTemperatureReached = true;
				}
			
			int engine_mod = -1;
			if(veh.GetModCount(VehicleMod.Engine) > 0) {
				engine_mod = veh.GetMod(VehicleMod.Engine);
				}
			
			//Active cooling
			if(vdata.IsFanRunning) {
				//Turn off fan if cooled enough
				if(vdata.EngineTemperature < FanDeactivationTemperature) vdata.IsFanRunning = false;

				//If still running, reduce engine temperature
				else {
					float fan_efficiency = temperature_loss_fan;
					if(temperature_loss_multipliers.ContainsKey((VehicleHash)veh.Model.Hash))
						fan_efficiency *= temperature_loss_multipliers[(VehicleHash)veh.Model.Hash];
					
					if(engine_mod >= 0) fan_efficiency *= (1.0f + temperature_engine_upgrade_bonus[engine_mod]);
					
					gained_temperature -= fan_efficiency * EnvironmentCoolingMult(veh);
					}
				}
			else {
				//Turn on fan if running hot
				if(vdata.EngineTemperature > FanActivationTemperature) vdata.IsFanRunning = true;
				}
			
			//Air cooling
			float air_cooling = forward_velocity * temperature_loss_air_cooling;
			if(engine_mod >= 0) air_cooling *= (1.0f + temperature_engine_upgrade_bonus[engine_mod]);
			//TODO specific cooling parts per model (e.g., intercoolers, hood vents, etc.)
			gained_temperature -= air_cooling;

			//Temperature inflow/outflow rates are measured per minute, not per second.
			gained_temperature /= 60;
			gained_temperature *= fps_multiplier;
			
			vdata.EngineTemperature += gained_temperature;
			}
		else {
			float environment = EnvironmentTemperature(veh);
			if(Ped.Exists(Game.Player.Character)) {
				if(vdata.IsKeyedOff && Game.Player.Character.CurrentVehicle != veh) {
					//Clear keyed off flag if player is no longer driving the vehicle
					vdata.IsKeyedOff = false;
					}
				}
			
			float lost_temperature = temperature_loss_off / 60 * fps_multiplier;
			
			//Switch fan off
			if(vdata.IsFanRunning) vdata.IsFanRunning = false;
			
			//Natural cooling while engine off
			if(vdata.EngineTemperature >= environment + lost_temperature) {
				vdata.EngineTemperature -= lost_temperature;
				}
			else {
				vdata.EngineTemperature = environment;
				}
			
			//Reset operating temperature flag if engine cools below 80% of normal.
			if(vdata.OperatingTemperatureReached && vdata.EngineTemperature < OperatingTemperature * ReheatTemperature) {
				vdata.OperatingTemperatureReached = false;
				}
			}
		
		float prorated_overheat_damage = 0f;
		if(veh.EngineHealth > 0) {
			if(vdata.EngineTemperature > temperature_overheat_threshold && vdata.EngineTemperature < temperature_engine_failure_threshold) {
				Core.PlayerVehicleDebug(veh.FriendlyName + " overheating!", veh);
				if(vdata.EngineTemperature < temperature_burn_oil_threshold) {
					prorated_overheat_damage = (vdata.EngineTemperature - temperature_overheat_threshold) / (temperature_burn_oil_threshold - temperature_overheat_threshold) * temperature_overheat_damage;
					}
				else if(vdata.EngineTemperature < temperature_engine_failure_threshold) {
					prorated_overheat_damage = temperature_overheat_damage + ((vdata.EngineTemperature - temperature_burn_oil_threshold) / (temperature_engine_failure_threshold - temperature_burn_oil_threshold)) * temperature_burn_oil_damage;
					}
				
				float overheat_damage_delta = prorated_overheat_damage - vdata.TemporaryOverheatDamage;
				if(overheat_damage_delta >= veh.EngineHealth)
					DestroyEngine(veh, vdata);
				else {
					veh.EngineHealth -= overheat_damage_delta;
					vdata.LastEngineHealth = veh.EngineHealth;
					}
				vdata.TemporaryOverheatDamage = prorated_overheat_damage;
				}
			else if(vdata.EngineTemperature >= temperature_engine_failure_threshold && veh.EngineRunning) {
				Core.PlayerVehicleDebug(veh.FriendlyName + " over engine failure threshold!", veh);
				if(temperature_engine_failure_instantaneous) DestroyEngine(veh, vdata);
				else {
					float bricking_damage = temperature_engine_failure_damage * fps_multiplier;
					if(bricking_damage >= veh.EngineHealth) DestroyEngine(veh, vdata);
					else {
						veh.EngineHealth -= bricking_damage;
						vdata.LastEngineHealth = veh.EngineHealth;
						}
					}
				}
			}
		
		/*
		if(Core.IsPlayerDriving && Core.IsPlayerVehicle(veh)) {
			float temp = (EnvironmentTemperature(veh)+STP_TEMPERATURE-273.15f);
			UI.ShowSubtitle("Zone: " + World.GetZoneNameLabel(veh.Position) +
				" Amb: " + temp.ToString("F2") + " °C" +
				" Temp: " + vdata.EngineTemperature.ToString("f5") + " (K+STP) " +
				//" Fan: " + vdata.IsFanRunning +
				//" Delta: " + gained_temperature.ToString("f5") + " (K/frame) " +
				//" Vel: " + forward_velocity.ToString("f2") +
				//" DMG: " + prorated_overheat_damage +
				" ENG: " + veh.EngineHealth.ToString("f2")
				);
			}
		*/
		}
	
	///<summary>Kills <paramref name="veh"/>'s engine, clearing any recovery data from <paramref name="vdata"/> (the engine is bricked).</summary>]
	///<remarks>Sets the vehicle to be fireproof (for one loop of <see cref="Core.ProcessVehicle(Vehicle, VehicleData, int)"/>) to prevent the engine from igniting on destruction.</remarks>
	public static void DestroyEngine(Vehicle veh, VehicleData vdata) {
		if(Core.IsPlayerVehicle(veh)) UI.Notify("~r~" + veh.FriendlyName + " engine destroyed!~s~");
		GTA.Audio.PlaySoundFromEntity(veh, "EX_POPS_BACKFIRE");
		if(!veh.IsFireProof) vdata.TemporaryFireproofing(veh, true);
		vdata.LastEngineHealth = veh.EngineHealth = 0f;
		veh.EngineRunning = false;
		vdata.Dead = true;
		veh.IsDriveable = false;
		vdata.TemporaryOverheatDamage = 0;
		}
	
	///<summary>Triggers some internal housekeeping whenever the engine is repaired (polled and called by ProcessEngine()).</summary>
	public static void HandleRepair(Vehicle veh, VehicleData vdata) {
		if(vdata.Dead) {
			vdata.Dead = false;
			veh.IsDriveable = true;
			}
		vdata.LastEngineHealth = veh.EngineHealth;
		vdata.LastBodyHealth = veh.BodyHealth;
		vdata.LastTankHealth = veh.PetrolTankHealth;
		vdata.TemporaryOverheatDamage = 0;
		vdata.IsBoilingOver = false;
		vdata.IsBurningOil = false;
		vdata.EngineTemperature = Math.Min(OperatingTemperature, vdata.EngineTemperature);
		}
	
	///<summary>Prevents vehicle from exploding when on fire by constantly repairing negative-health engine and fuel tank.</summary>
	///<remarks>Depending on processing rate, vehicles may not be repaired fast enough to prevent an explosion.</remarks>
	public static void HandleAntiExplosion(Vehicle veh, VehicleData vdata) {
		if(veh.IsOnFire && anti_explosion) {
			if(veh.EngineHealth < -500f) {
				if(!unexplained_fires_enabled || !unexplained_fire_models.Contains((VehicleHash)veh.Model.Hash)) {
					vdata.LastEngineHealth = veh.EngineHealth = -500f;
					}
				}
			if(veh.PetrolTankHealth < -100f) {
				vdata.LastTankHealth = veh.PetrolTankHealth = -100f;
				}
			}
		}
	
	///<summary>Offers a low chance per loop that the vehicle will burst into flames when inverted.</summary>
	public static void UnexplainedFire(Vehicle veh, float fps_multiplier) {
		if(veh.IsUpsideDown && !unexplained_fire_models.Contains((VehicleHash)veh.Model.Hash)) {
			if(Core.RandPercentage() < fps_multiplier) {
				Vector3 engine_pos = veh.GetBoneCoord("engine");
				Function.Call(Hash.START_SCRIPT_FIRE, engine_pos.X, engine_pos.Y, engine_pos.Z, /*maxChildren*/1, /*isGasFire*/true);

				}
			}
		}
	
	///<summary>Applies rapid cooling effect when player is blasting fire extinguisher at the engine block.</summary>
	public static void CoolWithFireExtinguisher(Vehicle veh, VehicleData vdata, float fpsmult = 1.00f) {
		//SHVDN raycasting doesn't seem to work for me, so I wrote my own raycast here
		Vector3 muzzle_point = Game.Player.Character.Weapons.CurrentWeaponObject.Position;
			
		//I have literally no idea why I need the RightVector rather than the ForwardVector, but the RightVector works
		// and the ForwardVector doesn't; if I had to guess, the x,y,z model is swapped with an x,z,y model?
		//Honestly, I have no idea what I'm doing with raycasting...
		Vector3 muzzle_direction = Vector3.Normalize(Game.Player.Character.Weapons.CurrentWeaponObject.RightVector);
		
		Vector3 veh_offset = Vector3.Subtract(muzzle_point, veh.GetBoneCoord("engine"));

		float sphere_radius_square = 1.0f;
		float dist_square = veh_offset.LengthSquared();
		float component_a = Vector3.Dot(veh_offset, muzzle_direction);
		float intersect_square = dist_square - (component_a * component_a);
			
		//UI.ShowSubtitle("x: " + veh_offset.X + ", y: " + veh_offset.Y + ", dist^2:" + dist_square);
		
		if(sphere_radius_square - (dist_square - component_a * component_a) > 0.0f || dist_square < intersect_square) {
			vdata.EngineTemperature -= Math.Min(vdata.EngineTemperature, fire_extinguisher_cooling_rate * fpsmult);
			//UI.ShowSubtitle(vdata.EngineTemperature.ToString());
			}
		}
	public static void ApplyImmersionCooling(Vehicle veh, VehicleData vdata, float fpsmult = 1.00f) {
		if(veh.Model.IsBoat) return; //boats ironically do not qualify for immersion cooling -- their engines never hit the water line
		if(immersion_cooling_rate > 0) {
			float submerged = GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_ENTITY_SUBMERGED_LEVEL, (Entity)veh);
			if(submerged > 0f) {
				float temp = vdata.EngineTemperature - Math.Min(vdata.EngineTemperature, submerged * immersion_cooling_rate * fpsmult);
				vdata.EngineTemperature = Math.Max(EnvironmentTemperature(veh) - 4f, temp);
				}
			}
		}
	public static void ApplyBonusDamage(Vehicle veh, VehicleData vdata, float damage) {
		if(bonus_damage_immune_models.Contains((VehicleHash)veh.Model.Hash)) return;
		damage *= bonus_damage_multiplier;
		float bonus_damage = (float)(Math.Pow(damage, bonus_damage_exponent) - damage) * (Core.RandPercent()*(bonus_damage_random_max - bonus_damage_random_min)+bonus_damage_random_min);
		Core.DebugNotify("Bonus damage calc: " + bonus_damage.ToString("F2"), Core.DebugLevel.ALL);

		if(veh.EngineHealth > 0) {
			//Don't bother with minuscule amounts of damage (they'll just accumulate rounding error anyway)
			if(bonus_damage > 1f) {
				Core.DebugNotify(veh.FriendlyName + " suffered " + Convert.ToInt32(bonus_damage).ToString("d") + " bonus damage", Core.DebugLevel.ALL);
				if(bonus_damage > veh.EngineHealth) {
					DestroyEngine(veh, vdata);
					}
				else {
					veh.EngineHealth -= bonus_damage;
					if(veh.EngineHealth <= 0) DestroyEngine(veh, vdata);
					}
				}
			}
		}
	
	///<summary>Returns offset of the local environment from <see cref="Engine.STP_TEMPERATURE"/>.</summary>
	///<remarks>This generally applies only to "cold start" temperature.</remarks>
	public static float EnvironmentTemperature(Vehicle veh) {
		if(!Vehicle.Exists(veh)) return 0f;
		float temp = 0f;
		switch(World.GetZoneNameLabel(veh.Position)) {
			//Don't be fooled by the "f" symbol; that's for "floating point", not "Fahrenheit"
			//(I want to throttle the ever-loving crap out of whoever thought it was a good idea to have to qualify ALL
			// floating point literals with an "f" symbol instead of letting the compiler presume that literals being
			// assigned to a floating point variable are, you know, FLOATING POINT.)
			case "DESRT": { temp = 10f; break; } //Grand Senora Desert (35 °C/95 °F)
			case "SANDY": { temp = 10f; break; } //Sandy Shores (35 °C/95 °F)
			case "GRAPES": { temp = 5f; break; } //Grapeseed (30 °C/85 °F)
			case "SANCHIA": { temp = 5f; break; } //San Chianski (30 °C/85 °F)
			case "PALFOR": { temp = -5f; break; } //Paleto Forest (20 °C/70 °F)
			case "PALETO": { temp = -5f; break; } //Paleto Bay (20 °C/70 °F)
			case "MTCHIL": { temp = -5f; break; } //Mt. Chiliad Wilderness (20 °C/70 °F)
			}
		if(veh.Position.X > 3000 && veh.Position.Y < -4500) {
			temp = -25f; //North Yankton (0 °C/30 °F) (actual temperatures in winter would be -15 °C/5 °F, but block heaters are assumed)
			}
		
		temp -= veh.Position.Z/100f; //hard coded: -1 K per 100 m ASL, per real rule of thumb
		
		//TODO: rain, storm, etc. weathers will also change heating/cooling rates
		
		//Time of day -- very weak system, simply an 8 K diurnal range
		float gamehour = World.CurrentDayTime.Hours + (World.CurrentDayTime.Minutes/60f) + (World.CurrentDayTime.Seconds/3600f);
		//0 = -5 degrees
		//2 = -6 degrees
		//6 = -8 degrees
		//10 = -4 degrees
		//14 = +0 degrees
		//18 = -2 degrees
		//22 = -4 degrees
		//24 = -5 degrees
		if(gamehour < 2.0f) temp -= 5 + gamehour/2.0f;
		else if(gamehour < 6.0f) temp -= 6 + (gamehour-2.0f)/2.0f;
		else if(gamehour < 14.0f) temp -= 8 - (gamehour-6.0f);
		else if(gamehour < 22.0f) temp -= 8 + (gamehour-14.0f)/2.0f;
		else temp -= 4 + (gamehour-22.0f)/2.0f;

		//Sanity limit: ambient temperature will never be colder than the upper limit of troposphere, nor warmer than hottest desert on Earth
		temp = Math.Min(Math.Max(MINIMUM_AMBIENT_TEMPERATURE - STP_TEMPERATURE, temp), MAXIMUM_AMBIENT_TEMPERATURE - STP_TEMPERATURE);

		//Immersion percentage applies linear -4 degree modifier
		float submerged = GTA.Native.Function.Call<float>(GTA.Native.Hash.GET_ENTITY_SUBMERGED_LEVEL, (Entity)veh);
		if(submerged > 0f) temp -= submerged * 4.0f;

		return temp;
		}
	public static float EnvironmentCoolingMult(Vehicle veh) {
		float mult = 1.0f;
		switch(World.GetZoneNameLabel(veh.Position)) {
			case "DESRT": { mult = 0.85f; break; } //Grand Senora Desert
			case "SANDY": { mult = 0.85f; break; } //Sandy Shores
			case "GRAPES": { mult = 0.9f; break; } //Grapeseed
			case "SANCHIA": { mult = 0.9f; break; } //San Chianski
			case "PALFOR": { mult = 1.05f; break; } //Paleto Forest
			case "PALETO": { mult = 1.05f; break; } //Paleto Bay
			case "MTCHIL": { mult = 1.05f; break; } //Mt. Chiliad Wilderness
			}
		if(veh.IsInAir) {
			float altitude_agl = veh.Position.Z - World.GetGroundHeight(veh.Position);
			float percent_sq = (altitude_agl / 10f) * (altitude_agl / 10f);
			if(percent_sq > 1.0f) percent_sq = 1.0f;
			if(percent_sq < 0f) percent_sq = 0f;
			mult += percent_sq * 0.1f;
			}
			
		//TODO: rain, storm, etc. weathers will also change heating/cooling rates

		return mult;
		}
	public static float EnvironmentHeatingMult(Vehicle veh) {
		float mult = 1.0f;
		switch(World.GetZoneNameLabel(veh.Position)) {
			case "DESRT": { mult = 1.15f; break; } //Grand Senora Desert
			case "SANDY": { mult = 1.15f; break; } //Sandy Shores
			case "GRAPES": { mult = 1.1f; break; } //Grapeseed
			case "SANCHIA": { mult = 1.1f; break; } //San Chianski
			case "PALFOR": { mult = 0.95f; break; } //Paleto Forest
			case "PALETO": { mult = 0.95f; break; } //Paleto Bay
			case "MTCHIL": { mult = 0.95f; break; } //Mt. Chiliad Wilderness
			}
		if(veh.IsInAir) {
			float altitude_agl = veh.Position.Y - World.GetGroundHeight(veh.Position);
			float percent_sq = (altitude_agl / 10f) * (altitude_agl / 10f);
			if(percent_sq > 1.0f) percent_sq = 1.0f;
			if(percent_sq < 0f) percent_sq = 0f;
			mult -= percent_sq * 0.1f;
			}
		
		//TODO: rain, storm, etc. weathers will also change heating/cooling rates

		return mult;
		}
}

}
