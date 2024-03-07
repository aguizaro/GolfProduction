using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class AnimationStateController : NetworkBehaviour
{
    Animator animator;

    // Start is called before the first frame update
    void Start()
    {
        animator = GetComponent<Animator>();
    }

    // Update is called once per frame
    void Update()
    {
        bool isrunning = animator.GetBool("isRunning");
        bool isStrafingLeft = animator.GetBool("isLeft");
        bool isStrafingRight = animator.GetBool("isRight");
        bool isWalking = animator.GetBool("isWalking");
        bool isReversing = animator.GetBool("isReversing");
        bool forwardPressed = Input.GetKey("w");
        bool runPressed = Input.GetKey("left shift");
        bool backPressed = Input.GetKey("s");
        bool rightPressed = Input.GetKey("d");
        bool leftPressed = Input.GetKey("a");
        bool strikePressed = Input.GetKeyDown("e");

        if (UIManager.isPaused) { return; }

        if(IsOwner){
            if (forwardPressed && !isWalking)
            {
                animator.SetBool("isWalking", true);
            }
            if (!forwardPressed && isWalking)
            {
                animator.SetBool("isWalking", false);
            }

            if (backPressed && !isReversing)
            {
                animator.SetBool("isReversing", true);
            }
            if (!backPressed && isReversing)
            {
                animator.SetBool("isReversing", false);
            }

            if (!isrunning && (forwardPressed && runPressed))
            {
                animator.SetBool("isRunning", true);
            }
            if (isrunning && (!runPressed || !forwardPressed))
            {
                animator.SetBool("isRunning", false);
            }

            if (leftPressed && !isStrafingLeft)
            {
                animator.SetBool("isLeft", true);
            }
            if (!leftPressed && isStrafingLeft)
            {
                animator.SetBool("isLeft", false);
            }

            if (rightPressed && !isStrafingRight)
            {
                animator.SetBool("isRight", true);
            }
            if (!rightPressed && isStrafingRight)
            {
                animator.SetBool("isRight", false);
            }

            if (strikePressed)
            {
                animator.SetBool("isStriking", true);
            }
            if (!strikePressed)
            {
                animator.SetBool("isStriking", false);
            }
        }
    }
}
