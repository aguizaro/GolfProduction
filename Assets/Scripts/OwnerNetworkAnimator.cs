using Unity.Netcode.Components;

// gives owener authority over network animations
public class OwnerNetworkAnimator : NetworkAnimator
{
    protected override bool OnIsServerAuthoritative()
    {
        return false;
    }
}