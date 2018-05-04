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

///<summary>This module enables assignment of transmissions from ikt's "Manual Transmission and Steering Wheel" mod
/// for each vehicle type.  Each vehicle will have a plausible transmission type given to it.</summary>
public sealed class Transmission : Script
{
	public Transmission() {
		if(transmission_assignment_enabled) {
			Tick += TransmissionTick;
			Interval = 33;
			}
		}
	
	///// TRANSMISSION /////
	// * Disabled by default (even if Manual Transmission mod is installed).
	// * Assigns realistic transmission by vehicle type when using ikt's "Manual Transmission and Steering Wheel" mod.
	// * Trains, planes, helicopters, boats, and bicycles (but not motorcycles) are ignored by this system.
	// * Manual Transmission mod is of course NOT REQUIRED to use Realism: Vehicles.  However, it offers such
	//    enhancements to realistic vehicle gameplay that even mouse-and-keyboard users are encouraged to give it a
	//    try -- using a mouse and keyboard with the sequential transmission can be almost as fun as driving stick!
	// * Manual Transmission mod has its own settings in its menu (by default assigned to the left bracket key) which
	//    you will need to configure before the mods can interoperate.  Before attempting to use any of these features,
	//    be sure that you have a working setup in the Manual Tranmission mod.
	// * (Tip) Although the default LCtrl/LShift gear controls work fine, LSPDFR users may want to change those keys
	//    to LCtrl/LAlt to avoid accidental cancellation of traffic stops when shifting gears.
	// * "Manual" transmissions will be Sequential by default.  You will want to change this setting if using a
	//    steering wheel with H-shifter; the default was selected to keep gamepad users (a significantly larger
	//    population of gamers) from having to reconfigure Realism: Vehicles before being able to try out the Manual
	//    Transmission compatibility features "straight out of the box".
	// * All options and frequencies are tweakable to your heart's content, of course.
	// * Note that the script only "fires" when the player enters a new vehicle.  All NPCs are presumed competent with
	//    the transmission in their vehicle and will operate it as if it were an automatic, even if it's a manual
	//    transmission.

	// * After entering a vehicle for the first time, its transmission will be assigned from a relevant whitelist; if
	//    it doesn't appear in a whitelist, it will be randomly assigned either a Manual or Automatic transmission
	//    according to its model, if a specific frequency is given for that model, or its class if its specific model
	//    doesn't specify.
	// * Most vehicles do appear in whitelists and therefore won't be randomly assigned transmissions, having been
	//    painstakingly researched (over 10 hours of effort!) based on the transmissions offered for their real-
	//    world equivalents.  Only those vehicles which assuredly come in both manual and automatic versions are
	//    omitted from the whitelists to allow random assignment.

	//Loading Transmission
	// * The transmission type is memorised according to plate and internal handle.  If the plate changes but the
	//    handle remains the same, the old association is deleted; if the handle changes, the same transmission is
	//    assigned to the vehicle with the new handle.

	//Choosing Transmission
	// * If a vehicle spawns without an owner or driver and does not need to be hotwired, and that vehicle does not
	///   have a specific transmission by default, you will be prompted to choose the transmission you want for
	///   that vehicle upon entry.  This accommodates trainers as well as mods which allow you to purchase vehicles.
	
	///<summary>Whether the mod will automatically assign transmissions to all vehicles.  If false, </summary>
	public static bool Enabled {
		get { return transmission_assignment_enabled; }
		set { transmission_assignment_enabled = value; }
		}
	
	///<summary>Has ikt's Manual Transmission mod been detected running on your computer? (Implementation: is the
	/// player's current vehicle currently showing variables that are assigned by the Manual Transmission
	/// mod?)</summary>
	public static bool ManualTransmissionModPresent {
		get { return manual_transmission_mod_detected; }
		}
	
	///<summary>Unrealistic. Should <see cref="manumatic_vehicles"/> use the H-shifter mode rather than the Sequential
	/// mode (when switched out of Automatic)? Good for people who really love their manual transmissions, but still
	/// want to be trapped in a pit of despair by the occasional vehicle that is strictly automatic.</summary>
	public static bool ManumaticUseHPattern {
		get { return manumatic_hpattern; }
		set { manumatic_hpattern = value; }
		}
	///<summary>Unrealistic. Should <see cref="semiauto_vehicles"/> use the H-shifter mode rather than the Sequential
	/// mode? Good for people who really love their manual transmissions, but still want to be trapped in a pit of
	/// despair by the occasional vehicle that is strictly automatic.</summary>
	public static bool SemiAutoUseHPattern {
		get { return semiauto_hpattern; }
		set { semiauto_hpattern = value; }
		}
	///<summary>Unrealistic. Should <see cref="sequential_vehicles"/> use the H-shifter mode rather than the Sequential
	/// mode? Good for people who really love their manual transmissions, but still want to be trapped in a pit of
	/// despair by the occasional vehicle that is strictly automatic.</summary>
	public static bool SequentialUseHPattern {
		get { return sequential_hpattern; }
		set { sequential_hpattern = value; }
		}
	
	///<summary>Should cars with manual transmissions use the Sequential mode rather than the H-shifter mode? Good for
	/// gamepad and mouse and keyboard users. Enabled by default: disable if you are using an H-shifter and prefer to
	/// use it.</summary>
	public static bool ManualUseSequential {
		get { return manual_sequential; }
		set { manual_sequential = value; }
		}
	
	///<summary>Should motorbikes always be Sequential, I-don't-give-a-damn-what-the-other-settings-say? (Includes trikes and quadbikes.)</summary>
	public static bool MotorcyclesAlwaysSequential {
		get { return motorbikes_sequential; }
		set { motorbikes_sequential = value; }
		}
	
	///<summary>If true, sequential transmissions can also be switched into full automatic (they behave as semi-auto
	/// transmissions).  If false, they can only operate in sequential mode.</summary>
	public static bool SequentialCanUseAutomatic {
		get { return sequential_switch; }
		set { sequential_switch = value; }
		}
		
	///<summary>If true, vehicles that are exclusively automatic in reality will also be exclusively automatic in the
	/// game.  If false, these vehicles will be given manual transmissions randomly per <see cref="generic_manual_frequency"/>.</summary>
	public static bool EnforceAutomaticVehicles {
		get { return enforce_automatic_vehicles; }
		set { enforce_automatic_vehicles = value; }
		}
	///<summary>If true, vehicles that are exclusively manual in reality will also be exclusively manual in the game.
	/// If false, these vehicles will be given automatic transmissions randomly per <see cref="generic_manual_frequency"/>.</summary>
	public static bool EnforceManualVehicles {
		get { return enforce_manual_vehicles; }
		set { enforce_manual_vehicles = value; }
		}
	
	///<summary>If true, all automatic sportscars can be switched from Automatic to Sequential (but not to
	/// H-shifter).  If false, only manumatics or sport automatics can.</summary>
	public static bool SportShifterEnabled {
		get { return enable_sport_shifter; }
		set { enable_sport_shifter = value; }
		}
	
	///<summary>Percentage (0.0 to 100.0) of vehicles in a certain class having a manual transmission, unless
	/// they appear in a specific whitelist and thereby offer no choice of a manual or automatic.</summary>
	public static Dictionary<VehicleClass, float> ManualFrequencyByClass {
		get { return class_manual_frequencies; }
		}
	///<summary>Dictionary of specific models along with their specific percentage (0.0 to 100.0) that they will use a
	/// manual transmission. Overrides <see cref="class_manual_frequencies"/> for that model.</summary>
	public static Dictionary<VehicleHash, float> ManualFrequencyByModel {
		get { return specific_manual_frequencies; }
		}
	
	///<summary>Percentage chance (0.0 to 100.0) that an unspecified class or vehicle spawns with a manual transmission.</summary>
	public static float ManualFrequency {
		get { return generic_manual_frequency; }
		}
	
	internal static bool transmission_assignment_enabled = true;
	
	internal static Keys switch_shift_mode_key = Keys.Oem1;
	internal static Core.KeyboardMeta switch_shift_mode_metakeys = Core.KeyboardMeta.None;

	internal static bool manual_transmission_mod_detected = false;
	
	public const string MT_GEAR = "mt_gear";
	public const string MT_SET_SHIFT_MODE = "mt_set_shiftmode";
	public const string MT_GET_SHIFT_MODE = "mt_get_shiftmode";
	
	internal static bool manumatic_hpattern = false;
	internal static bool semiauto_hpattern = false;
	internal static bool sequential_hpattern = false;
	internal static bool manual_sequential = true;

	internal static bool motorbikes_sequential = false;

	internal static bool sequential_switch = false;
		
	internal static bool enforce_automatic_vehicles = true;
	internal static bool enforce_manual_vehicles = true;

	internal static bool enable_sport_shifter = true;
		
	internal static Dictionary<VehicleClass, float> class_manual_frequencies = new Dictionary<VehicleClass, float>() {
		{ VehicleClass.Commercial, 0f }, /* Urban medium duty vehicles almost universally automatic */
		{ VehicleClass.Compacts, 15.0f }, /* Manual transmissions cheaper, but most are still automatics */
		{ VehicleClass.Coupes, 12.0f }, /* Slightly higher frequency of manuals than sedans */
		{ VehicleClass.Emergency, 0f }, /* Police/fire/medical vehicles universally automatic */
		{ VehicleClass.Industrial, 65.0f }, /* American truckers still prefer manual gearboxes */
		{ VehicleClass.Military, 0f }, /* Military vehicles universally automatic */
		{ VehicleClass.Muscle, 50.0f }, /* Muscle cars were a tossup between manual and auto */
		{ VehicleClass.OffRoad, 75.0f }, /* Manual gear selection important for offroading */
		{ VehicleClass.Sedans, 7.0f }, /* Extremely commonly automatic */
		{ VehicleClass.Sports, 25.0f }, /* Most modern sportscars are driven by lazy people... */
		{ VehicleClass.SportsClassics, 80.0f }, /* Old sportscars were "better" in manual */
		{ VehicleClass.Super, 50.0f }, /* If they offer you a choice, it's a coin toss */
		{ VehicleClass.SUVs, 5.0f }, /* "Soccer moms" -- overwhelmingly automatic */
		{ VehicleClass.Utility, 5.0f }, /* Urban utility vehicles rarely are manual */
		{ VehicleClass.Vans, 8.0f } /* Manual transmissions only occasionally preferred */
		};
	
	internal static Dictionary<VehicleHash, float> specific_manual_frequencies = new Dictionary<VehicleHash, float>() {
		{ VehicleHash.Mesa, 25.0f }, /* Classed as "SUV", so requires custom weighting for off-road */
		{ VehicleHash.Mesa2, 65.0f }, /* North Yankton variant more likely to be manual */
		{ VehicleHash.Peyote, 65.0f },
		{ VehicleHash.Surfer2, 25.0f }, /* Older VW vans commonly manual */
		{ VehicleHash.TrophyTruck, 25.0f }, /* Baja raiders are usually automatic */
		{ VehicleHash.TrophyTruck2, 25.0f },
		{ VehicleHash.Warrener, 90.0f } /* Euro manufacturer in spite of Japanese style -- took artistic liberty here */
		};
	
	internal static float generic_manual_frequency = 0f;
	
	///<summary>List of all vehicles which start in Automatic mode but can be toggled into Sequential mode (or
	/// H-shifter mode if <see cref="manumatic_hpattern"/> is true).</summary>
	internal static List<VehicleHash> manumatic_vehicles = new List<VehicleHash>() {
		VehicleHash.Adder, VehicleHash.Cog55, VehicleHash.Cog552, VehicleHash.CogCabrio,
		VehicleHash.Cognoscenti, VehicleHash.Cognoscenti2, VehicleHash.Dubsta3, VehicleHash.Exemplar, VehicleHash.F620,
		VehicleHash.FBI, VehicleHash.FBI2, VehicleHash.Buffalo, VehicleHash.Felon,
		VehicleHash.Felon2, VehicleHash.Feltzer2, VehicleHash.ItaliGTB, VehicleHash.ItaliGTB2, VehicleHash.Jackal,
		VehicleHash.Limo2, VehicleHash.Massacro, VehicleHash.Massacro2, VehicleHash.Nero, VehicleHash.Nero2,
		VehicleHash.Oracle, VehicleHash.Pfister811, VehicleHash.Prototipo, VehicleHash.RapidGT, VehicleHash.RapidGT2,
		VehicleHash.Reaper, VehicleHash.Rocoto, VehicleHash.Seven70, VehicleHash.Sheava, VehicleHash.Specter,
		VehicleHash.Specter2, VehicleHash.Surano, VehicleHash.T20, VehicleHash.Tailgater, VehicleHash.Tempesta,
		VehicleHash.Turismor, VehicleHash.Vagner, VehicleHash.Vigilante /* 1989 Batmobile was automatic, but I figure
		an unusually cool and generous one-percenter like Batman would swap in a manumatic DCT just because he's
		awesome */, VehicleHash.Visione, VehicleHash.Zentorno
		};
	
	///<summary>List of all vehicles which have Automatic transmissions with sport/paddle shift.  These are only
	/// cosmetically different from Manumatics in terms of gameplay, but vastly different in terms of actual
	/// mechanical operation.</summary>
	internal static List<VehicleHash> sport_auto_vehicles = new List<VehicleHash>() {
		VehicleHash.Buffalo2
		};
	
	///<summary>List of all vehicles which operate exclusively in Sequential mode (or H-shifter if
	/// <see cref="sequential_hpattern"/> is true), unless <see cref="sequential_switch"/> is true where they
	/// can also be switched to Automatic.</summary>
	internal static List<VehicleHash> sequential_vehicles = new List<VehicleHash>() {
		//Motorcycles
		VehicleHash.Akuma, VehicleHash.Bagger, VehicleHash.Blazer, VehicleHash.Blazer2, VehicleHash.Blazer3,
		VehicleHash.Blazer4, VehicleHash.Blazer5, VehicleHash.CarbonRS, VehicleHash.Chimera, VehicleHash.Cliffhanger,
		VehicleHash.Daemon, VehicleHash.Daemon2, VehicleHash.Defiler, VehicleHash.Diablous, VehicleHash.Diablous2,
		VehicleHash.Double, VehicleHash.Enduro, VehicleHash.Esskey, VehicleHash.FCR, VehicleHash.FCR2,
		VehicleHash.Gargoyle, VehicleHash.Hakuchou, VehicleHash.Hakuchou2, VehicleHash.Hexer, VehicleHash.Innovation,
		VehicleHash.Lectro, VehicleHash.Manchez, VehicleHash.Nemesis, VehicleHash.Nightblade, VehicleHash.Oppressor,
		VehicleHash.PCJ, VehicleHash.RatBike, VehicleHash.Ruffian, VehicleHash.Sanchez, VehicleHash.Sanchez2,
		VehicleHash.Sanctus, VehicleHash.Shotaro, VehicleHash.Thrust, VehicleHash.Vader, VehicleHash.Vindicator,
		VehicleHash.Vortex, VehicleHash.Wolfsbane, VehicleHash.ZombieA, VehicleHash.ZombieB
		};
	
	///<summary>List of all vehicles which operate in Sequential mode (or H-shifter if <see cref="semiauto_hpattern"/>
	/// is true) but can be toggled into Automatic mode.</summary>
	internal static List<VehicleHash> semiauto_vehicles = new List<VehicleHash>() {
		VehicleHash.Cheetah, VehicleHash.Cheetah2, VehicleHash.Comet2, VehicleHash.Dubsta, VehicleHash.Dubsta2,
		VehicleHash.Elegy2, VehicleHash.FMJ, VehicleHash.Furoregt, VehicleHash.Infernus,
		VehicleHash.Jester, VehicleHash.Jester2, VehicleHash.LE7B, VehicleHash.Osiris, VehicleHash.Panto,
		VehicleHash.RallyTruck, VehicleHash.Raptor
		};
	
	///<summary>List of all vehicles which always have Automatic transmissions. These tend to be duty vehicles
	/// and boring vehicles... Q.E.D.</summary>
	internal static List<VehicleHash> automatic_vehicles = new List<VehicleHash>() {
		VehicleHash.Dilettante, VehicleHash.Dilettante2, VehicleHash.Brawler, VehicleHash.Caddy, VehicleHash.Caddy2,
		VehicleHash.Caddy3, VehicleHash.Faggio, VehicleHash.Faggio2, VehicleHash.Faggio3, VehicleHash.Forklift,
		VehicleHash.FQ2, VehicleHash.Fugitive, VehicleHash.Gresley, VehicleHash.Khamelion, VehicleHash.Limo2,
		VehicleHash.Lurcher, VehicleHash.Manana, VehicleHash.Minivan, VehicleHash.Minivan2, VehicleHash.NightShark,
		VehicleHash.Oracle2, VehicleHash.Patriot, VehicleHash.Pony, VehicleHash.Pony2, VehicleHash.Pounder,
		VehicleHash.Primo, VehicleHash.Primo2,
		VehicleHash.Radi /*1st gen were automatic only, only the second gen offered manual transmission*/,
		VehicleHash.Rhino, VehicleHash.Riot, VehicleHash.Ripley, VehicleHash.Romero, VehicleHash.Rumpo,
		VehicleHash.Rumpo2, VehicleHash.Rumpo3, VehicleHash.Sandking, VehicleHash.Sandking2, VehicleHash.Seminole,
		VehicleHash.Serrano, VehicleHash.Stanier, VehicleHash.Stretch, VehicleHash.Superd, VehicleHash.Surge,
		VehicleHash.Taxi, VehicleHash.Virgo, VehicleHash.Virgo2, VehicleHash.Virgo3, VehicleHash.Voodoo,
		VehicleHash.Voodoo2, VehicleHash.Voltic, VehicleHash.Voltic2, VehicleHash.Washington, VehicleHash.Windsor,
		VehicleHash.Windsor2, VehicleHash.XA21, VehicleHash.XLS, VehicleHash.XLS2,
		//Military
		VehicleHash.Brickade, VehicleHash.Barracks, VehicleHash.Barracks2, VehicleHash.Barracks3,
		VehicleHash.Insurgent, VehicleHash.Insurgent2, VehicleHash.Insurgent3,
		//Emergency
		VehicleHash.Ambulance, VehicleHash.PBus, VehicleHash.Police /*Stanier*/, VehicleHash.Police2 /*Buffalo*/,
		VehicleHash.Police3 /*Interceptor*/, VehicleHash.Police4 /*Unmarked*/, VehicleHash.PoliceOld1,
		VehicleHash.PoliceOld2, VehicleHash.PoliceT, VehicleHash.Pranger, VehicleHash.Sheriff, VehicleHash.Sheriff2,
		//Trucks/Utility
		VehicleHash.Airbus, VehicleHash.Airtug, VehicleHash.FireTruck, VehicleHash.Handler, VehicleHash.Mule,
		VehicleHash.Mule2, VehicleHash.Mule3, VehicleHash.Mower, VehicleHash.RentalBus, VehicleHash.Stockade,
		VehicleHash.Stockade3, VehicleHash.Taco, VehicleHash.Tourbus, VehicleHash.Trash, VehicleHash.Trash2,
		VehicleHash.UtilityTruck, VehicleHash.UtilityTruck2, VehicleHash.UtilityTruck3,
		};
	
	///<summary>List of all vehicles which will always have Manual transmissions (or Sequential if
	/// <see cref="manual_sequential"/> is true).</summary>
	internal static List<VehicleHash> manual_vehicles = new List<VehicleHash>() {
		//Cars/Light Trucks
		VehicleHash.Banshee, VehicleHash.Banshee2, VehicleHash.BfInjection, VehicleHash.Bifta, VehicleHash.BType,
		VehicleHash.BType2, VehicleHash.BType3, VehicleHash.Buffalo3, VehicleHash.Carbonizzare, VehicleHash.Casco,
		VehicleHash.Comet3, VehicleHash.Coquette2, VehicleHash.Coquette3, VehicleHash.Elegy, VehicleHash.Feltzer3,
		VehicleHash.GP1, VehicleHash.Habanero, VehicleHash.Hakuchou, VehicleHash.Hotknife, VehicleHash.Infernus2,
		VehicleHash.JB700, VehicleHash.Mamba, VehicleHash.Monroe, VehicleHash.Omnis, VehicleHash.Penetrator,
		VehicleHash.Phoenix, VehicleHash.RapidGT3, VehicleHash.RatLoader, VehicleHash.RatLoader2, VehicleHash.Retinue,
		VehicleHash.Ruston, VehicleHash.Stinger, VehicleHash.StingerGT, VehicleHash.SultanRS, VehicleHash.Torero,
		VehicleHash.Tropos, VehicleHash.Turismo2, VehicleHash.Tyrus, VehicleHash.Verlierer2,
		//Trucks/Utility
		/*VehicleHash.Barracks, VehicleHash.Barracks2, VehicleHash.Barracks3,*/ VehicleHash.Biff, VehicleHash.Bulldozer,
		VehicleHash.DLoader, VehicleHash.Docktug, VehicleHash.Dump, VehicleHash.HalfTrack, VehicleHash.Mixer,
		VehicleHash.Mixer2, VehicleHash.Scrap, VehicleHash.TipTruck, VehicleHash.TipTruck2, VehicleHash.Tractor,
		VehicleHash.Tractor2, VehicleHash.Tractor3
		};
	
	private static int _last_vehicle = 0;
	
	private static void TransmissionTick(object sender, EventArgs parameters) {
		if(Core.IsPlayerDriving) {
			Vehicle veh = Game.Player.Character.CurrentVehicle;
			if(!manual_transmission_mod_detected) {
				int gear = Core.GetIntDecorator(veh, MT_GEAR);
				if(gear == 0) return;
				manual_transmission_mod_detected = true;
				}
			
			if(_last_vehicle == 0 || _last_vehicle != veh.Handle) {
				_last_vehicle = veh.Handle;
				AssignTransmission(veh, Core.VehicleData(veh));
				}
			}
		}
	
	private static void TransmissionKeyDown(object sender, KeyEventArgs parameters) {
		if(Core.IsPlayerDriving) {
			Vehicle veh = Game.Player.Character.CurrentVehicle;
			VehicleData vdata = Core.VehicleData(veh);
			if(Core.MatchModifiers(switch_shift_mode_metakeys, parameters) && parameters.KeyCode == switch_shift_mode_key) {
				ToggleTransmissionMode(veh, vdata);
				}
			}
		}
	
	public static void AssignTransmission(Vehicle veh, VehicleData vdata) {
		if(veh.Model.IsBicycle || veh.Model.IsBoat || veh.Model.IsHelicopter || veh.Model.IsPlane || veh.Model.IsTrain) return;
		
		//TODO: LOAD TRANSMISSION BY LICENCE PLATE
		
		//TODO: OPEN TRANSMISSION SELECTOR FOR NEWLY SPAWNED VEHICLES

		if(vdata.TransmissionAssigned) {
			ReportTransmissionType(vdata.TransmissionType);
			return;
			}

		Core.ShifterMode mode = Core.ShifterMode.AUTOMATIC;
		Core.TransmissionType tranny = Core.TransmissionType.NONE;

		if(veh.ClassType == VehicleClass.Motorcycles && motorbikes_sequential)  tranny = Core.TransmissionType.SEQUENTIAL;
		else if(manual_vehicles.Contains((VehicleHash)veh.Model.Hash))     tranny = Core.TransmissionType.MANUAL;
		else if(manumatic_vehicles.Contains((VehicleHash)veh.Model.Hash))  tranny = Core.TransmissionType.MANUMATIC;
		else if(sequential_vehicles.Contains((VehicleHash)veh.Model.Hash)) tranny = Core.TransmissionType.SEMIAUTO;
		else if(automatic_vehicles.Contains((VehicleHash)veh.Model.Hash))  tranny = Core.TransmissionType.AUTOMATIC;

		switch(tranny) {
			case(Core.TransmissionType.MANUAL): {
				if(enforce_manual_vehicles) {
					if(!manual_sequential) InstallTransmission(veh, vdata, Core.ShifterMode.HPATTERN, Core.TransmissionType.MANUAL);
					else InstallTransmission(veh, vdata, Core.ShifterMode.SEQUENTIAL, Core.TransmissionType.MANUAL);
					return;
					}
				break; //Fallthrough to random gearbox
				}
			case (Core.TransmissionType.SEQUENTIAL): {
				if(sequential_hpattern) InstallTransmission(veh, vdata, Core.ShifterMode.HPATTERN, Core.TransmissionType.SEQUENTIAL);
				else InstallTransmission(veh, vdata, Core.ShifterMode.SEQUENTIAL, Core.TransmissionType.SEQUENTIAL);
				return;
				}
			case (Core.TransmissionType.SEMIAUTO): {
				if(semiauto_hpattern) InstallTransmission(veh, vdata, Core.ShifterMode.HPATTERN, Core.TransmissionType.SEMIAUTO);
				else InstallTransmission(veh, vdata, Core.ShifterMode.SEQUENTIAL, Core.TransmissionType.SEMIAUTO);
				return;
				}
			case(Core.TransmissionType.AUTOMATIC_SPORT): {
				InstallTransmission(veh, vdata, Core.ShifterMode.AUTOMATIC, Core.TransmissionType.AUTOMATIC_SPORT);
				return;
				}
			case (Core.TransmissionType.MANUMATIC): {
				InstallTransmission(veh, vdata, Core.ShifterMode.AUTOMATIC, Core.TransmissionType.MANUMATIC);
				return;
				}
			case (Core.TransmissionType.AUTOMATIC): {
				if(enforce_automatic_vehicles) {
					InstallTransmission(veh, vdata, Core.ShifterMode.AUTOMATIC, Core.TransmissionType.AUTOMATIC);
					return;
					}
				break; //Fallthrough to random gearbox
				}
			}
		
		//Random gearbox; install a manual or automatic based on population frequencies
		//We need to determine which manual transmission type the player prefers (sequential or H-pattern)
		Core.ShifterMode manual = Core.ShifterMode.HPATTERN;
		if(manual_sequential) manual = Core.ShifterMode.SEQUENTIAL;
		
		float rand = Core.RandPercentage();
		float target = generic_manual_frequency;
		if(specific_manual_frequencies.ContainsKey((VehicleHash)veh.Model.Hash)) {
			target = specific_manual_frequencies[(VehicleHash)veh.Model.Hash];
			}
		else if(class_manual_frequencies.ContainsKey(veh.ClassType)) {
			target = class_manual_frequencies[veh.ClassType];
			}
		
		if(rand < target) {
			mode = manual;
			tranny = Core.TransmissionType.MANUAL;
			}
		else {
			mode = Core.ShifterMode.AUTOMATIC;
			tranny = Core.TransmissionType.AUTOMATIC;
			}
		InstallTransmission(veh, vdata, mode, tranny);
		}

	public static void ReportTransmissionType(Core.TransmissionType tranny) {
		switch(tranny) {
			case (Core.TransmissionType.MANUAL): {
				UI.Notify("Transmission: Manual");
				break;
				}
			case (Core.TransmissionType.SEQUENTIAL): {
				UI.Notify("Transmission: Sequential");
				break;
				}
			case (Core.TransmissionType.SEMIAUTO): {
				UI.Notify("Transmission: Semi-Automatic");
				break;
				}
			case (Core.TransmissionType.MANUMATIC): {
				UI.Notify("Transmission: Manumatic");
				break;
				}
			case (Core.TransmissionType.AUTOMATIC): {
				UI.Notify("Transmission: Automatic");
				break;
				}
			}
		}
	
	public static void InstallTransmission(Vehicle veh, VehicleData vdata, Core.ShifterMode mode, Core.TransmissionType tranny) {
		ReportTransmissionType(tranny);
		vdata.TransmissionType = tranny;
		Core.SetIntDecorator(veh, MT_SET_SHIFT_MODE, (int)mode);
		}
	
	public static void ToggleTransmissionMode(Vehicle veh, VehicleData vdata) {
		if(!vdata.TransmissionAssigned) AssignTransmission(veh, vdata);
		
		Core.ShifterMode mode = (Core.ShifterMode)Core.GetIntDecorator(veh, MT_GET_SHIFT_MODE);
		Core.TransmissionType tranny = vdata.TransmissionType;
		if(tranny == Core.TransmissionType.MANUAL) {
			mode = manual_sequential ? Core.ShifterMode.SEQUENTIAL : Core.ShifterMode.HPATTERN;
			}
		else if(tranny == Core.TransmissionType.SEQUENTIAL) {
			mode = sequential_hpattern ? Core.ShifterMode.HPATTERN : Core.ShifterMode.SEQUENTIAL;
			}
		else if(tranny == Core.TransmissionType.AUTOMATIC) {
			mode = Core.ShifterMode.AUTOMATIC;
			}
		else {
			mode = mode == Core.ShifterMode.SEQUENTIAL ? mode = Core.ShifterMode.AUTOMATIC : mode = Core.ShifterMode.SEQUENTIAL;
			}
		Core.SetIntDecorator(veh, MT_SET_SHIFT_MODE, (int)mode);
		}
}
//*/

}