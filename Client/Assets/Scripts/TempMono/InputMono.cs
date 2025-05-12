using Lockstep.Collision2D;
using Lockstep.Logic;
using Lockstep.Math;
using UnityEngine;
using UnityEngine.Serialization;
using Debug = Lockstep.Logging.Debug;

namespace LockstepTutorial {

    public class InputMono : UnityEngine.MonoBehaviour {
        private static bool IsReplayMode => GameEntry.Instance.IsReplayMode;
        [HideInInspector] public int floorMask;
        public float camRayLength = 100;

        public bool hasHitFloor;
        public LVector2 mousePos;
        public LVector2 mouseUV;
        public bool isInputFire;
        public int skillId;
        public bool isSprint;

        void Start(){
            floorMask = LayerMask.GetMask("Floor");
        }

        public void Update(){
            if (!IsReplayMode) {
                float h = Input.GetAxisRaw("Horizontal");
                float v = Input.GetAxisRaw("Vertical");
                mouseUV = new LVector2(h.ToLFloat(), v.ToLFloat());

                isInputFire = Input.GetButton("Fire1");
                hasHitFloor = Input.GetMouseButtonDown(1);
                if (hasHitFloor) {
                    Ray camRay = Camera.main.ScreenPointToRay(Input.mousePosition);
                    RaycastHit floorHit;
                    if (Physics.Raycast(camRay, out floorHit, camRayLength, floorMask)) {
                        mousePos = floorHit.point.ToLVector2XZ();
                    }
                }

                skillId = -1;
                for (int i = 0; i < 6; i++) {
                    if (Input.GetKeyDown(KeyCode.Alpha1 + i)) {
                        skillId = i;
                    }
                }

                isSprint = Input.GetKeyDown(KeyCode.Space);
                GameEntry.CurrentGameInput =  new PlayerInput() {
                    MousePos = mousePos,
                    InputUV = mouseUV,
                    IsSprint = isSprint,
                    IsFire = isInputFire
                };
                
            }
        }
    }
}