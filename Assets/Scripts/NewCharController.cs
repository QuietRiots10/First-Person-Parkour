using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class NewCharController : MonoBehaviour
{
    //Player Objects
    [Tooltip("LookDir object of player")]
    public GameObject LookDir;
    [Tooltip("Player's Rigidbody")]
    public Rigidbody PlayerBody;

    //Input and Player States
    float x, y; //Axis data
    public bool OnGround, OnWall, Jumping, Sprinting, Crouching; //Player states

    //Movement
    private Vector2 InputVector = Vector2.zero;
    [Tooltip("How quickly the player moves (Default = 4500)")]
    public float moveSpeed;
    [Tooltip("Maximum speed of player, factoring in sliding and stuff (Default = 20)")]
    public float maxSpeed;
    [Tooltip("LayerMask for ground objects")]
    public LayerMask WhatIsGround;

    //Slowdown
    [Tooltip("Controls how quickly speed is lost while moving normally (Default = 0.175)")]
    public float MoveSlowdown;
    private float Threshold = 0.01f;
    [Tooltip("Maximum slope angle for walkable surfaces (Default = 35)")]
    public float MaxSlopeAngle;

    //Crouch & Slide
    private Vector3 crouchScale = new Vector3(1, 0.5f, 1);
    private Vector3 playerScale;
    [Tooltip("Force added when a slide is started (Default = 400)")]
    public float SlideForce;
    [Tooltip("Controls how quickly speed is lost while sliding (Default = 0.2)")]
    public float SlideSlowdown;

    //Jumping
    private bool ReadyToJump = true;
    private float JumpCooldown = 0.25f;
    [Tooltip("Controls how high the player jumps (Default = 550)")]
    public float JumpForce;

    //Sliding
    private Vector3 normalVector = Vector3.up;

    //Wallrunning
    public GameObject LastWallTouched; //Contains the last wall touched (MAY CAUSE PROBLEMS IN CORNERS?)
    public GameObject LastWallJumped; //Contains the last wall jumped off of
    private bool ReadyToWallJump;

    void Start()
    {
        playerScale = transform.localScale;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    //FixedUpdate is used for movement
    private void FixedUpdate()
    {
        Movement();
        Deathplane();
    }
   
    //Fetches input data from Unity's Input System
    public void OnMove(InputAction.CallbackContext context)
    {
        InputVector = context.ReadValue<Vector2>();
    }
    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.started) Jumping = true;
        if (context.canceled) Jumping = false;
    }
    public void OnCrouch(InputAction.CallbackContext context)
    {
        if (context.started) StartCrouch();
        if (context.canceled) StopCrouch();
    }
    public void OnSprint(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            Sprinting = true;            
        }
        if (context.canceled)
        {
            Sprinting = false;
        }
    }


    /// <summary>
    /// Processes all player movement, including jumping, sliding, and slowdown.
    /// </summary>
    /// <returns></returns>
    private void Movement()
    {
        x = InputVector.x;
        y = InputVector.y;

        //Extra gravity just for the player
        PlayerBody.AddForce(Vector3.down * Time.deltaTime * 10);

        //Find actual velocity relative to where player is looking
        Vector2 mag = FindVelRelativeToLook();
        float xMag = mag.x, yMag = mag.y;

        //Counteract sliding and sloppy movement, slow down the player
        Slowdown(x, y, mag);

        //If holding jump then jump
        if (Jumping) Jump();

        //Set max speed
        float maxSpeed = this.maxSpeed;

        //If sliding down a ramp, add force down so player stays grounded and also builds speed
        if (Crouching && OnGround && ReadyToJump)
        {
            PlayerBody.AddForce(Vector3.down * Time.deltaTime * 3000);
            return;
        }

        //If speed is larger than maxspeed, cancel out the input so you don't go over max speed
        if (x > 0 && xMag > maxSpeed) x = 0;
        if (x < 0 && xMag < -maxSpeed) x = 0;
        if (y > 0 && yMag > maxSpeed) y = 0;
        if (y < 0 && yMag < -maxSpeed) y = 0;

        //Some multipliers on MoveSpeed
        float Multiplier = 1f, MultiplierForward = 1f;

        //Air strafing
        if (!OnGround)
        {
            Multiplier = 0.5f;
            MultiplierForward = 1f;
        }

        //Movement while sliding
        if (OnGround && Crouching) MultiplierForward = 0f;

        //Apply forces
        PlayerBody.AddForce(LookDir.transform.forward * y * moveSpeed * Time.deltaTime * Multiplier * MultiplierForward);
        PlayerBody.AddForce(LookDir.transform.right * x * moveSpeed * Time.deltaTime * Multiplier);
    }

    private void StartCrouch()
    {
        Crouching = true;

        //Shrinks player hitbox
        transform.localScale = crouchScale;
        transform.position = new Vector3(transform.position.x, transform.position.y - 0.5f, transform.position.z);
        
        //Speeds player up when they start a slide
        if (PlayerBody.velocity.magnitude > 0.5f)
        {
            if (OnGround) PlayerBody.AddForce(LookDir.transform.forward * SlideForce);
        }
    }

    private void StopCrouch()
    {
        Crouching = false;

        //Restores player to normal size
        transform.localScale = playerScale;
        transform.position = new Vector3(transform.position.x, transform.position.y + 0.5f, transform.position.z);
    }

    private void Jump()
    {
        //Grounded Jumps
        if (OnGround && ReadyToJump)
        {
            ReadyToJump = false;

            //Add jump forces
            PlayerBody.AddForce(Vector2.up * JumpForce * 1.5f);
            PlayerBody.AddForce(normalVector * JumpForce * 0.5f);

            //If jumping while falling, reset y velocity.
            Vector3 vel = PlayerBody.velocity;
            if (PlayerBody.velocity.y < 0.5f)
                PlayerBody.velocity = new Vector3(vel.x, 0, vel.z);
            else if (PlayerBody.velocity.y > 0)
                PlayerBody.velocity = new Vector3(vel.x, vel.y / 2, vel.z);

            Invoke(nameof(ResetJump), JumpCooldown);
        }

        //Wall jumps
        else if (OnWall && (LastWallTouched !=  LastWallJumped))
        {
            Debug.Log("Wall Jump!");
            LastWallJumped = LastWallTouched;

            //Add jump forces
            PlayerBody.AddForce(Vector2.up * JumpForce * 1.8f);
            PlayerBody.AddForce(normalVector * JumpForce * 0.75f);

            //If jumping while falling, reset y velocity.
            Vector3 vel = PlayerBody.velocity;
            if (PlayerBody.velocity.y < 0.5f)
                PlayerBody.velocity = new Vector3(vel.x, 0, vel.z);
            else if (PlayerBody.velocity.y > 0)
                PlayerBody.velocity = new Vector3(vel.x, vel.y / 2, vel.z);
        }
    }

    private void ResetJump()
    {
        ReadyToJump = true;
    }


    /// <summary>
    /// Slows down the player, mimicing the effects of friction.
    /// </summary>
    /// <returns></returns>
    private void Slowdown(float x, float y, Vector2 mag)
    {
        //Does not apply while airborne
        if (!OnGround || Jumping) return;

        //Apply slowdown while sliding
        if (Crouching)
        {
            PlayerBody.AddForce(moveSpeed * Time.deltaTime * -PlayerBody.velocity.normalized * SlideSlowdown);
            return;
        }

        //Apply slowdown to normal movement
        //Checks if input (x,y) is sufficiently small, or magnitude of velocity is below threshold
        if (Mathf.Abs(mag.x) > Threshold && Mathf.Abs(x) < 0.05f || (mag.x < -Threshold && x > 0) || (mag.x > Threshold && x < 0))
        {
            PlayerBody.AddForce(moveSpeed * LookDir.transform.right * Time.deltaTime * -mag.x * MoveSlowdown);
        }
        if (Mathf.Abs(mag.y) > Threshold && Mathf.Abs(y) < 0.05f || (mag.y < -Threshold && y > 0) || (mag.y > Threshold && y < 0))
        {
            PlayerBody.AddForce(moveSpeed * LookDir.transform.forward * Time.deltaTime * -mag.y * MoveSlowdown);
        }

        
        //Limit diagonal running. This will also cause a full stop if sliding fast and un-crouching, so not optimal.
        //TODO: Optimize this shit. Square roots, powers, really past Austin?! There must be a better way than this.
        if (Mathf.Sqrt((Mathf.Pow(PlayerBody.velocity.x, 2) + Mathf.Pow(PlayerBody.velocity.z, 2))) > maxSpeed)
        {
            float fallspeed = PlayerBody.velocity.y;
            Vector3 n = PlayerBody.velocity.normalized * maxSpeed;
            PlayerBody.velocity = new Vector3(n.x, fallspeed, n.z);
        }
        
    }

    /// <summary>
    /// Find the velocity relative to where the player is looking. 
    /// Useful for vectors calculations regarding movement and limiting movement.
    /// </summary>
    /// <returns></returns>
    public Vector2 FindVelRelativeToLook()
    {
        float lookAngle = LookDir.transform.eulerAngles.y;
        float moveAngle = Mathf.Atan2(PlayerBody.velocity.x, PlayerBody.velocity.z) * Mathf.Rad2Deg;

        float u = Mathf.DeltaAngle(lookAngle, moveAngle);
        float v = 90 - u;

        float magnitue = PlayerBody.velocity.magnitude;
        float yMag = magnitue * Mathf.Cos(u * Mathf.Deg2Rad);
        float xMag = magnitue * Mathf.Cos(v * Mathf.Deg2Rad);

        return new Vector2(xMag, yMag);
    }


    //Floor detection
    private bool cancellingOnGround; //For cancelling OnGround state when you jump
    private bool cancellingOnWall; //For cancelling OnWall state when you jump

    private void OnCollisionStay(Collision other)
    {
        //Make sure we are only checking for ground layers
        if (WhatIsGround != (WhatIsGround | (1 << other.gameObject.layer))) return;

        //Iterate through every collision
        for (int i = 0; i < other.contactCount; i++)
        {
            other.GetContacts(other.contacts);
            foreach (ContactPoint c in other.contacts)
            {
                //If angle of surface normal is less than MaxSlopeAngle, then the surface is considered "Ground"
                if (Vector3.Angle(Vector3.up, c.normal) < MaxSlopeAngle)
                {
                    OnGround = true;
                    normalVector = c.normal;

                    //Clears referenced wall
                    LastWallTouched = null;
                    LastWallJumped = null;


                    cancellingOnGround = false;
                    CancelInvoke(nameof(StopOnGround));
                }
                //If angle of surface normal is between 89 and 91, then the surface is considered "Wall"
                else if (Vector3.Angle(Vector3.up, c.normal) > 89 && Vector3.Angle(Vector3.up, c.normal) < 91)
                {
                    OnWall = true;
                    Jumping = false;
                    normalVector = Vector3.Normalize(c.normal * 1.5f + Vector3.up);

                    //Updates referenced wall to be the GameObject you're touching (MAY CAUSE PROBLEMS IN CORNERS?)
                    LastWallTouched = c.otherCollider.gameObject;

                    cancellingOnWall = false;
                    CancelInvoke(nameof(StopOnWall));
                }
            }    
        }

        //Invoke ground/wall cancel, since we can't check normals with CollisionExit
        float delay = 3f;
        if (!cancellingOnGround)
        {
            cancellingOnGround = true;
            Invoke(nameof(StopOnGround), Time.deltaTime * delay);
        }
        if (!cancellingOnWall)
        {
            cancellingOnWall = true;
            Invoke(nameof(StopOnWall), Time.deltaTime * delay);
        }

        //Make OnWall and OnGround mutually exclusive
        if (OnGround && OnWall)
        {
            OnWall = false;

            //Clears referenced wall
            LastWallTouched = null;
            LastWallJumped = null;
        }
    }

    private void StopOnGround()
    {
        OnGround = false;
    }
    private void StopOnWall()
    {
        OnWall = false;
    }

    private void Deathplane()
    {
        //If player falls below y=-10 bring them back to origin
        if (transform.position.y <= -10)
        {
            transform.position = Vector3.zero;
            Debug.Log("You fell lol");
        }
    }

}

