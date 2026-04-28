using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
	public class StarterAssetsInputs : MonoBehaviour
	{
		[Header("Character Input Values")]
		public Vector2 move;
		public Vector2 look;
		public bool jump;
		public bool sprint;
		public bool punch;
		public bool charge;
		public bool interaction;
		public bool selfHarm;
		public bool crouch;
        public bool shift;
		public bool pause;


		[Header("Movement Settings")]
		public bool analogMovement;

		[Header("Mouse Cursor Settings")]
		public bool cursorLocked = true;
		public bool cursorInputForLook = true;

#if ENABLE_INPUT_SYSTEM
		public void OnMove(InputValue value)
		{
			MoveInput(value.Get<Vector2>());
		}

		public void OnLook(InputValue value)
		{
			if (cursorInputForLook)
			{
				LookInput(value.Get<Vector2>());
			}
		}

		public void OnJump(InputValue value)
		{
			JumpInput(value.isPressed);
		}

		public void OnSprint(InputValue value)
		{
			SprintInput(value.isPressed);
		}

		// 펀치, 차징키 추가
		public void OnPunch(InputValue value) { PunchInput(value.isPressed); }
		public void OnCharge(InputValue value) { ChargeInput(value.isPressed); }
        public void OnInteraction(InputValue value) { InteractionInput(value.isPressed); }
        public void OnSelfHarm(InputValue value) { SelfHarmInput(value.isPressed); }
        public void OnCrouch(InputValue value) { CrouchInput(value.isPressed); }  // 추가
        public void OnShift(InputValue value)  { ShiftInput(value.isPressed); }   // 추가
		public void OnPause(InputValue value) { PauseInput(value.isPressed); }
#endif

		public void MoveInput(Vector2 newMoveDirection)
		{
			move = newMoveDirection;
		}

		public void LookInput(Vector2 newLookDirection)
		{
			look = newLookDirection;
		}

		public void JumpInput(bool newJumpState)
		{
			jump = newJumpState;
		}

		public void SprintInput(bool newSprintState)
		{
			sprint = newSprintState;
		}

		private void OnApplicationFocus(bool hasFocus)
		{
			SetCursorState(cursorLocked);
		}

		private void SetCursorState(bool newState)
		{
			Cursor.lockState = newState ? CursorLockMode.Locked : CursorLockMode.None;
		}


		public void PunchInput(bool newPunchState) { punch = newPunchState; }
		public void ChargeInput(bool newChargeState) { charge = newChargeState; }
        public void InteractionInput(bool newInteractionState) { interaction = newInteractionState; }
        public void SelfHarmInput(bool newSelfHarmState) { selfHarm = newSelfHarmState; }
	    public void CrouchInput(bool newCrouchState) { if (newCrouchState) crouch = !crouch; } 
		
        public void ShiftInput(bool newShiftState) { shift = newShiftState; }
		
		public void PauseInput(bool newPauseState) { if(newPauseState) pause = !pause; }
    }
	
}