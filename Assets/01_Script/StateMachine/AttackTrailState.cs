using UnityEngine;
using Tiny;

public class AttackTrailState : StateMachineBehaviour
{
    private Trail trail;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (trail == null)
            trail = animator.GetComponentInChildren<Trail>(true);

        if (trail != null)
            trail.enabled = true;
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (trail != null)
        {
            trail.Clear();
            trail.enabled = false;
        }
    }
}