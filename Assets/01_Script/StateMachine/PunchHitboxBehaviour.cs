using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PunchHitboxBehaviour : StateMachineBehaviour
{
    private CharacterView _view;
    private CharacterLocalView _localView;

    override public void OnStateEnter(Animator animator, AnimatorStateInfo info, int layerIndex)
    {
        if (_view == null) 
            _view = animator.GetComponentInParent<CharacterView>();
        if (_localView == null) 
            _localView = animator.GetComponentInParent<CharacterLocalView>();
        
        _view?.EnableRightHandHitbox();
        _localView?.EnableRightHandHitbox();
    }

    override public void OnStateExit(Animator animator, AnimatorStateInfo info, int layerIndex)
    {
        _view?.DisableRightHandHitbox();
        _localView?.DisableRightHandHitbox();
    }
}