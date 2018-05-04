using GTA;
using GTA.Native;
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using XInputDotNetPure;
using System.Linq;

namespace RealismVehicles
{

///<summary>This module disables the ability to roll your vehicle while it is airborne or has landed in any
/// orientation other than upright.</summary>
///<remarks>Currently only disables for player.  Will investigate possibility of disabling controls for AI (and,
/// especially, unoccupied vehicles which still engage in infuriating self-righting behaviour).</remarks>
public sealed class Airborne : Script
{
	public Airborne() {
		Interval = 100;
		//Configuration.Load(this);
		Tick += AirborneTick;
		}

	///<summary>Enable/disable this module.</summary>
	public static bool Enable {
		get { return airborne_enabled; }
		set { airborne_enabled = value; }
		}

	///<summary>Whether vehicles can lose control as a result of G forces caused by orientation, even if
	/// their wheels are in contact with the ground.</summary>
	public static bool EnableTractionLoss {
		get { return G_loss_of_traction; }
		set { G_loss_of_traction = value; }
		}

	///<summary>If a vehicle is rotated to this lateral or reverse G level from upright, i.e., is oriented at this
	/// multiple of 90 degrees, it loses control.</summary>
	public static float GThreshold {
		get { return G_threshold; }
		set { G_threshold = value; }
		}
	
	///<summary>Can the Back to the Future-themed Deluxo car still be controlled in the air?  Disable if using a mod
	/// that removes flight mode.</summary>
	public static bool AllowDeluxoAirborneControl {
		get { return allow_deluxo; }
		set { allow_deluxo = value; }
		}
	
	///<summary>Can motorcycles and quadbikes still be controlled in the air?</summary>
	public static bool AllowMotorcycleAirborneControl {
		get { return allow_motorcycles; }
		set { allow_motorcycles = value; }
		}
	///<summary>Can boats can still be controlled in the air (most relevant for Seasharks)?</summary>
	public static bool AllowBoatAirborneControl {
		get { return allow_boats; }
		set { allow_boats = value; }
		}
		
	internal static bool airborne_enabled = true;
	
	internal static bool allow_deluxo = true;

	internal static bool G_loss_of_traction = true;
	internal static float G_threshold = 0.9f;

	internal static bool allow_motorcycles = true;
	internal static bool allow_boats = true;

	///<summary>Runtime. If true, the vehicle has no airborne control.</summary>
	private bool _dead_stick = false;

	private void AirborneTick(object sender, EventArgs parameters) {
		if(!Core.IsPlayerDriving) {
			if(_dead_stick) {
				_dead_stick = false;
				Interval = 100;
				}
			return;
			}

		Vehicle veh = Game.Player.Character.CurrentVehicle;
		if(veh.Model.IsBicycle) return;
		if(veh.Model.IsHelicopter || veh.Model.IsPlane) return;
		if(veh.Model.IsTrain) return;
		if(allow_motorcycles && (veh.Model.IsBike || veh.Model.IsQuadbike)) return;
		if(allow_boats && veh.Model.IsBoat) return;
		//Hardcoded exception for Deluxo (Back to the Future expy)
		if(veh.IsInAir && !veh.IsUpsideDown && IsUpright(veh) && allow_deluxo && (VehicleHash)veh.Model.Hash == VehicleHash.Deluxo) return;

		if(veh.IsInAir || veh.IsUpsideDown || IsSlipping(veh)) {
			if(!_dead_stick) {
				//Move to per-frame processing
				Interval = 0;
				_dead_stick = true;
				}
			Game.DisableControlThisFrame(0, GTA.Control.VehicleMoveUpDown);
			Game.DisableControlThisFrame(0, GTA.Control.VehicleMoveLeftRight);
			}
		else if(_dead_stick && veh.IsOnAllWheels) {
			//Drop back to 10 fps processing
			Interval = 100;
			_dead_stick = false;
			}
		}
	
	private bool IsUpright(Vehicle veh) {
		return Function.Call<bool>(Hash.IS_ENTITY_UPRIGHT, (Entity)veh, 90.0f * G_threshold);
		}

	///<summary>If vehicle is no longer upright with at least one of its wheels off the ground, or is skidding past the G threshold if the traction G-loss is implemented.</summary>
	private bool IsSlipping(Vehicle veh) {
		if(G_loss_of_traction) return !IsUpright(veh);
		return !IsUpright(veh) && !veh.IsOnAllWheels;
		}
	
}

}