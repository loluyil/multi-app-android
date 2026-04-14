using System.Collections.Generic;
using UnityEngine;

public class ThirteenMatchState
{
    public readonly struct PlayResult
    {
        public readonly bool Success;
        public readonly string Reason;
        public readonly ThirteenRules.AnalyzedHand Hand;

        public PlayResult(bool success, string reason, ThirteenRules.AnalyzedHand hand)
        {
            Success = success;
            Reason = reason;
            Hand = hand;
        }
    }

    private readonly bool[] passed = new bool[4];
    private readonly bool enforceTurnOrder;
    private bool openingThreeOfSpadesRequired = true;

    public int CurrentTurnSeat { get; private set; }
    public int LeadingSeat { get; private set; } = -1;
    public bool TrickIsOpen => !CurrentHand.IsValid;
    public ThirteenRules.AnalyzedHand CurrentHand { get; private set; }

    public ThirteenMatchState(int startingSeat = 0, bool enforceTurnOrder = true)
    {
        this.enforceTurnOrder = enforceTurnOrder;
        CurrentTurnSeat = Mathf.Clamp(startingSeat, 0, 3);
        CurrentHand = default;
    }

    public PlayResult CanPlay(int seat, IReadOnlyList<Card.CardData> cards)
    {
        if (!IsSeatValid(seat))
            return Fail("Seat is invalid.");

        if (HasPassed(seat) && CurrentHand.IsValid)
            return Fail($"Seat {seat} already passed and is out for this trick.");

        ThirteenRules.AnalyzedHand analyzedHand = ThirteenRules.Analyze(cards);
        if (!analyzedHand.IsValid)
            return Fail("Selected cards do not form a valid hand.", analyzedHand);

        if (enforceTurnOrder && seat != CurrentTurnSeat)
            return Fail($"It is not seat {seat}'s turn.", analyzedHand);

        if (openingThreeOfSpadesRequired && !ContainsThreeOfSpades(cards))
            return Fail("The opening play must include the 3 of spades.", analyzedHand);

        if (!ThirteenRules.CanPlayOn(analyzedHand, CurrentHand, out string reason))
            return Fail(reason, analyzedHand);

        return new PlayResult(true, "Play accepted.", analyzedHand);
    }

    public PlayResult TryPlay(int seat, IReadOnlyList<Card.CardData> cards)
    {
        PlayResult validation = CanPlay(seat, cards);
        if (!validation.Success)
            return validation;

        CurrentHand = validation.Hand;
        LeadingSeat = seat;
        CurrentTurnSeat = GetNextActiveSeat(seat);
        openingThreeOfSpadesRequired = false;

        Debug.Log($"[Thirteen] Seat {seat} played {ThirteenRules.Describe(validation.Hand)}");
        return validation;
    }

    public bool CanPass(int seat, out string reason)
    {
        if (!IsSeatValid(seat))
        {
            reason = "Seat is invalid.";
            return false;
        }

        if (enforceTurnOrder && seat != CurrentTurnSeat)
        {
            reason = $"It is not seat {seat}'s turn.";
            return false;
        }

        if (!CurrentHand.IsValid)
        {
            reason = "Cannot pass when the trick is empty.";
            return false;
        }

        if (seat == LeadingSeat)
        {
            reason = "The leading seat cannot pass on its own trick.";
            return false;
        }

        if (passed[seat])
        {
            reason = $"Seat {seat} already passed this trick.";
            return false;
        }

        reason = "Pass accepted.";
        return true;
    }

    public bool TryPass(int seat, out string reason)
    {
        if (!CanPass(seat, out reason))
            return false;

        passed[seat] = true;
        Debug.Log($"[Thirteen] Seat {seat} passed.");

        if (HaveAllOtherSeatsPassed())
        {
            Debug.Log($"[Thirteen] Trick reset. Seat {LeadingSeat} keeps control.");
            CurrentTurnSeat = LeadingSeat;
            CurrentHand = default;
            ClearPasses();
            reason = "All other seats passed. Trick reset.";
            return true;
        }

        CurrentTurnSeat = GetNextActiveSeat(seat);
        reason = "Pass accepted.";
        return true;
    }

    public bool HasPassed(int seat)
    {
        return IsSeatValid(seat) && passed[seat];
    }

    public void ResetTrick(int nextLeadSeat)
    {
        LeadingSeat = Mathf.Clamp(nextLeadSeat, 0, 3);
        CurrentTurnSeat = LeadingSeat;
        CurrentHand = default;
        ClearPasses();
        Debug.Log($"[Thirteen] Trick manually reset to seat {LeadingSeat}.");
    }

    private PlayResult Fail(string reason, ThirteenRules.AnalyzedHand hand = default)
    {
        Debug.Log($"[Thirteen] Play rejected: {reason}");
        return new PlayResult(false, reason, hand);
    }

    private bool HaveAllOtherSeatsPassed()
    {
        if (LeadingSeat < 0)
            return false;

        for (int seat = 0; seat < passed.Length; seat++)
        {
            if (seat == LeadingSeat)
                continue;

            if (!passed[seat])
                return false;
        }

        return true;
    }

    private int GetNextActiveSeat(int fromSeat)
    {
        int nextSeat = fromSeat;
        for (int i = 0; i < passed.Length; i++)
        {
            nextSeat = GetNextSeat(nextSeat);
            if (!passed[nextSeat])
                return nextSeat;
        }

        return LeadingSeat >= 0 ? LeadingSeat : GetNextSeat(fromSeat);
    }

    private static int GetNextSeat(int seat)
    {
        return (seat + 1) % 4;
    }

    private void ClearPasses()
    {
        for (int i = 0; i < passed.Length; i++)
            passed[i] = false;
    }

    private static bool IsSeatValid(int seat)
    {
        return seat >= 0 && seat < 4;
    }

    private static bool ContainsThreeOfSpades(IReadOnlyList<Card.CardData> cards)
    {
        if (cards == null)
            return false;

        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i].rank == 3 && cards[i].suit == Card.Suit.Spades)
                return true;
        }

        return false;
    }
}
