using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Palace.Core;

// Palace rules tests. Run with:  dotnet run   (from Tests/Palace.Core.Tests)
// Part 1: one scenario per rule in the brief. Part 2: thousands of full bot games.
internal static class Program
{
    private static int passed, failed;
    private static int nextId = 10000;

    private static int Main()
    {
        RuleScenarios();
        NetworkMessages();
        Simulations();
        BotLevelsAndTutorial();
        SwapBeforePlay();
        GameModes();
        Console.WriteLine();
        Console.WriteLine($"RESULT: {passed} passed, {failed} failed");
        return failed == 0 ? 0 : 1;
    }

    // ------------------------------------------------------------------ helpers

    private static Card C(Rank r, Suit s = Suit.Spades) => new Card(nextId++, r, s);

    /// <summary>A table where every player still has one face-down card (so nobody counts as finished).</summary>
    private static PalaceGame Table(int players, int current = 0)
    {
        var st = new GameState();
        for (int i = 0; i < players; i++)
        {
            var p = new PlayerState(i, "P" + i, false);
            p.FaceDown[0] = C(Rank.Three);
            st.Players.Add(p);
        }
        st.Current = current;
        return new PalaceGame(st, new HouseRules(), 1);
    }

    private static void Pile(PalaceGame g, params Rank[] ranks)
    {
        foreach (var r in ranks) g.State.Discard.Add(C(r, Suit.Hearts));
    }

    private static List<Card> Give(PalaceGame g, int player, params Rank[] ranks)
    {
        var cards = ranks.Select(r => C(r)).ToList();
        g.State.Players[player].Hand.AddRange(cards);
        return cards;
    }

    private static List<int> Ids(IEnumerable<Card> cards) => cards.Select(c => c.Id).ToList();

    private static void Check(bool condition, string name)
    {
        if (condition) passed++;
        else
        {
            failed++;
            Console.WriteLine("  FAIL: " + name);
        }
    }

    private static void Section(string title) => Console.WriteLine("- " + title);

    // ------------------------------------------------------------------ part 1

    private static void RuleScenarios()
    {
        Console.WriteLine("Rule scenarios");

        Section("Building upwards");
        {
            var g = Table(3);
            Pile(g, Rank.Nine);
            var hand = Give(g, 0, Rank.Five, Rank.Nine, Rank.Jack);
            Check(!g.CheckRank(Rank.Five).Legal, "5 on 9 is illegal");
            Check(g.CheckRank(Rank.Five).Reason == "Needs 9 or higher", "reason explains the requirement");
            Check(g.CheckRank(Rank.Nine).Legal, "equal value (9 on 9) is legal");
            Check(!g.PlayCards(0, PlaySource.Hand, Ids(new[] { hand[0] })).Ok, "engine rejects the 5");
            var r = g.PlayCards(0, PlaySource.Hand, Ids(new[] { hand[2] }));
            Check(r.Ok && g.State.Current == 1, "J is played and the turn passes to the next player");
        }

        Section("2 resets (King on top, hand 2/6/9)");
        {
            var g = Table(3);
            Pile(g, Rank.King);
            var hand = Give(g, 0, Rank.Two, Rank.Six, Rank.Nine);
            Check(!g.CheckRank(Rank.Six).Legal && !g.CheckRank(Rank.Nine).Legal, "6 and 9 can't go on a King");
            Check(g.CheckRank(Rank.Two).Legal, "2 can go on a King");
            g.PlayCards(0, PlaySource.Hand, Ids(new[] { hand[0] }));
            Check(g.CheckRank(Rank.Three).Legal, "after a 2 the next player can play anything (3)");
        }

        Section("7 HIGHER");
        {
            var g = Table(3);
            Pile(g, Rank.Five);
            var hand = Give(g, 0, Rank.Seven, Rank.Four);
            var r = g.PlayCards(0, PlaySource.Hand, Ids(new[] { hand[0] }));
            Check(r.Ok && g.State.Phase == GamePhase.AwaitingSevenCall, "playing a 7 asks for HIGHER/LOWER");
            Check(!g.PlayCards(1, PlaySource.Hand, new List<int> { 1 }).Ok, "nobody can play until the call is made");
            g.DeclareSeven(0, SevenCall.Higher);
            Check(g.State.Current == 1, "after the call it is the next player's turn");
            Check(g.CheckRank(Rank.Eight).Legal && g.CheckRank(Rank.Ace).Legal, "8 and Ace are higher than 7");
            Check(!g.CheckRank(Rank.Six).Legal && !g.CheckRank(Rank.Seven).Legal, "6 and 7 are not higher than 7");
            Check(g.CheckRank(Rank.Two).Legal, "2 can still be played on a 7");
            Check(g.CheckRank(Rank.Ten).Legal, "10 can burn on a HIGHER 7");
        }

        Section("7 LOWER");
        {
            var g = Table(3);
            var hand = Give(g, 0, Rank.Seven);
            g.PlayCards(0, PlaySource.Hand, Ids(hand));
            g.DeclareSeven(0, SevenCall.Lower);
            Check(g.CheckRank(Rank.Six).Legal && g.CheckRank(Rank.Three).Legal, "6 and 3 are lower than 7");
            Check(!g.CheckRank(Rank.Eight).Legal && !g.CheckRank(Rank.Seven).Legal, "8 and 7 are not lower");
            Check(!g.CheckRank(Rank.Ten).Legal && !g.AceBurnLegal, "10 / Ace can't burn on a LOWER 7 (default)");
            Check(g.CheckRank(Rank.Ace).Legal, "but an Ace can be played as a low card");
            Check(g.CheckRank(Rank.Ten).Reason == "Must be LOWER than 7", "reason explains the 7 call");
            Check(g.CheckRank(Rank.Two).Legal, "2 is allowed on a LOWER 7");
        }

        Section("7 call ends after the next player picks up");
        {
            var g = Table(3);
            var hand = Give(g, 0, Rank.Seven);
            Give(g, 1, Rank.King);
            g.PlayCards(0, PlaySource.Hand, Ids(hand));
            g.DeclareSeven(0, SevenCall.Lower);
            var r = g.PickUp(1);
            Check(r.Ok && g.State.Discard.Count == 0 && g.ActiveSeven == SevenCall.None, "pile collected, call cleared");
            Check(g.State.Current == 2, "turn passes after picking up");
        }

        Section("8 skips: 3 players, two 8s");
        {
            var g = Table(3);
            var hand = Give(g, 0, Rank.Eight, Rank.Eight, Rank.Four);
            var r = g.PlayCards(0, PlaySource.Hand, Ids(hand.Take(2)));
            var skip = r.Events.First(e => e.Type == GameEventType.PlayersSkipped);
            Check(skip.Players.SequenceEqual(new[] { 1, 2 }), "players 2 and 3 are skipped");
            Check(g.State.Current == 0, "player 1 gets another turn");
        }

        Section("8 skips: 5 players, two 8s");
        {
            var g = Table(5);
            var hand = Give(g, 0, Rank.Eight, Rank.Eight);
            g.PlayCards(0, PlaySource.Hand, Ids(hand));
            Check(g.State.Current == 3, "players 2 and 3 skipped, player 4 is next");
        }

        Section("8 skips: three 8s skip three players");
        {
            var g = Table(6);
            var hand = Give(g, 0, Rank.Eight, Rank.Eight, Rank.Eight);
            g.PlayCards(0, PlaySource.Hand, Ids(hand));
            Check(g.State.Current == 4, "players 2, 3 and 4 skipped, player 5 is next");
            Check(g.CheckRank(Rank.Seven).Legal == false && g.CheckRank(Rank.Eight).Legal, "next player must beat the 8");
        }

        Section("8 with only 2 players left is a normal card (Q24)");
        {
            var g = Table(2);
            var hand = Give(g, 0, Rank.Eight, Rank.Four);
            var r = g.PlayCards(0, PlaySource.Hand, Ids(hand.Take(1)));
            Check(r.Ok && !r.Events.Any(e => e.Type == GameEventType.PlayersSkipped), "2 players: an 8 skips nobody");
            Check(g.State.Current == 1, "2 players: the other player is next");
            Check(!g.CheckRank(Rank.Seven).Legal && g.CheckRank(Rank.Eight).Legal, "2 players: the 8 still has to be beaten");

            var g3 = Table(3);
            g3.State.Players[2].FaceDown[0] = null; // player 3 has finished
            g3.State.Players[2].FinishPosition = 1;
            g3.State.FinishOrder.Add(2);
            var h3 = Give(g3, 0, Rank.Eight, Rank.Eight, Rank.Four);
            g3.PlayCards(0, PlaySource.Hand, Ids(h3.Take(2)));
            Check(g3.State.Current == 1, "3 players with one finished: two 8s skip nobody");

            var g4 = Table(2);
            g4.Rules.EightSkipsWithTwoLeft = true;
            var h4 = Give(g4, 0, Rank.Eight, Rank.Four);
            g4.PlayCards(0, PlaySource.Hand, Ids(h4.Take(1)));
            Check(g4.State.Current == 0, "switch on: the 8 skips the other player as before");
        }

        Section("10 burns and gives another turn");
        {
            var g = Table(3);
            Pile(g, Rank.Four, Rank.Nine, Rank.King);
            var hand = Give(g, 0, Rank.Ten, Rank.Three);
            Check(g.CheckRank(Rank.Ten).Legal, "10 can go on a King");
            var r = g.PlayCards(0, PlaySource.Hand, Ids(hand.Take(1)));
            Check(r.Ok && g.State.Discard.Count == 0 && g.State.BurnedCount == 4, "pile (including the 10) is removed from play");
            Check(g.State.Current == 0, "same player goes again");
            Check(g.PlayCards(0, PlaySource.Hand, Ids(hand.Skip(1))).Ok, "and can now play anything (3)");
        }

        Section("Ace: high card or burn");
        {
            var g = Table(3);
            Pile(g, Rank.King);
            var hand = Give(g, 0, Rank.Ace, Rank.Five);
            g.PlayCards(0, PlaySource.Hand, Ids(hand.Take(1)));
            Check(g.State.Phase == GamePhase.AwaitingAceChoice, "playing an Ace asks high or burn");
            g.ChooseAce(0, true);
            Check(g.State.Discard.Count == 0 && g.State.Current == 0, "burn: pile gone, same player again");

            var g2 = Table(3);
            Pile(g2, Rank.King);
            var hand2 = Give(g2, 0, Rank.Ace);
            g2.PlayCards(0, PlaySource.Hand, Ids(hand2));
            g2.ChooseAce(0, false);
            Check(g2.State.TopRank == Rank.Ace && g2.State.Current == 1, "high: Ace stays on top, turn passes");
            Check(!g2.CheckRank(Rank.King).Legal && g2.CheckRank(Rank.Ace).Legal, "next must play an Ace...");
            Check(g2.CheckRank(Rank.Two).Legal && g2.CheckRank(Rank.Ten).Legal, "...or a 2 or 10");
        }

        Section("Ace as a chosen value: on a Jack it can be J/Q/K/A but never 9");
        {
            var g = Table(3);
            Pile(g, Rank.Jack);
            var hand = Give(g, 0, Rank.Ace);
            g.PlayCards(0, PlaySource.Hand, Ids(hand));
            var options = g.State.PendingAceValues;
            Check(options.SequenceEqual(new[] { Rank.Jack, Rank.Queen, Rank.King, Rank.Ace }), "options are J, Q, K, A");
            Check(g.State.PendingAceBurnAllowed, "burning is also offered");
            Check(!g.ChooseAce(0, false, Rank.Nine).Ok, "choosing 9 on a Jack is rejected");
            Check(g.ChooseAce(0, false, Rank.Queen).Ok, "choosing Queen is accepted");
            Check(g.State.Current == 1 && g.State.Direction == 1, "turn passes; the Ace-as-Queen does NOT reverse");
            Check(!g.CheckRank(Rank.Jack).Legal && g.CheckRank(Rank.Queen).Legal && g.CheckRank(Rank.King).Legal,
                "next player must play a Queen or higher");
            Check(g.Requirement() == "Q or higher", "requirement text shows the Ace's value");
        }

        Section("Ace on an empty pile can stand for any value except 10");
        {
            var g = Table(3);
            var hand = Give(g, 0, Rank.Ace);
            g.PlayCards(0, PlaySource.Hand, Ids(hand));
            Check(g.State.PendingAceValues.Count == 12 && !g.State.PendingAceValues.Contains(Rank.Ten), "2-9, J, Q, K, A");
            g.ChooseAce(0, false, Rank.Four);
            Check(g.CheckRank(Rank.Four).Legal && !g.CheckRank(Rank.Three).Legal, "next player must beat a 4");
        }

        Section("Ace on a HIGHER 7 must stand for 8 or more");
        {
            var g = Table(3);
            var seven = Give(g, 0, Rank.Seven);
            g.PlayCards(0, PlaySource.Hand, Ids(seven));
            g.DeclareSeven(0, SevenCall.Higher);
            var ace = Give(g, 1, Rank.Ace);
            g.PlayCards(1, PlaySource.Hand, Ids(ace));
            Check(g.State.PendingAceValues.First() == Rank.Eight && !g.State.PendingAceValues.Contains(Rank.Seven), "options start at 8");
        }

        Section("Ace on a LOWER 7 can be a low card, 2-6 (no burn)");
        {
            var g = Table(3);
            var seven = Give(g, 0, Rank.Seven);
            g.PlayCards(0, PlaySource.Hand, Ids(seven));
            g.DeclareSeven(0, SevenCall.Lower);
            var ace = Give(g, 1, Rank.Ace);
            Check(g.PlayCards(1, PlaySource.Hand, Ids(ace)).Ok, "Ace can be played on a LOWER 7");
            Check(g.State.PendingAceValues.SequenceEqual(new[] { Rank.Two, Rank.Three, Rank.Four, Rank.Five, Rank.Six }), "options are 2-6");
            Check(!g.State.PendingAceBurnAllowed, "burning isn't offered on a LOWER 7");
            Check(!g.ChooseAce(1, false, Rank.Eight).Ok, "choosing 8 is rejected");
            g.ChooseAce(1, false, Rank.Four);
            Check(g.CheckRank(Rank.Four).Legal && !g.CheckRank(Rank.Three).Legal, "next player must beat the Ace-as-4");
        }

        Section("Queen reverses");
        {
            var g = Table(4);
            var hand = Give(g, 0, Rank.Queen, Rank.Queen);
            g.PlayCards(0, PlaySource.Hand, Ids(hand.Take(1)));
            Check(g.State.Direction == -1 && g.State.Current == 3, "one Queen: direction flips, player 4 is next");

            var g2 = Table(4);
            var hand2 = Give(g2, 0, Rank.Queen, Rank.Queen);
            g2.PlayCards(0, PlaySource.Hand, Ids(hand2));
            Check(g2.State.Direction == 1 && g2.State.Current == 1, "two Queens: no change");

            var g3 = Table(2);
            var hand3 = Give(g3, 0, Rank.Queen);
            g3.PlayCards(0, PlaySource.Hand, Ids(hand3));
            Check(g3.State.Current == 1, "2 players: the other player is still next");
        }

        Section("Multiple cards are optional and must match");
        {
            var g = Table(3);
            var hand = Give(g, 0, Rank.Eight, Rank.Eight, Rank.Eight, Rank.Nine);
            Check(!g.PlayCards(0, PlaySource.Hand, Ids(new[] { hand[0], hand[3] })).Ok, "8 + 9 together is rejected");
            g.PlayCards(0, PlaySource.Hand, Ids(hand.Take(1)));
            Check(g.State.Players[0].Hand.Count(c => c.Rank == Rank.Eight) == 2, "playing one 8 keeps the other two");
        }

        Section("Picking up");
        {
            var g = Table(3);
            Pile(g, Rank.Five, Rank.Ace);
            Give(g, 0, Rank.Three, Rank.Four);
            Check(g.MustPickUp(0), "no legal card means the player must pick up");
            var r = g.PickUp(0);
            Check(r.Ok && g.State.Players[0].Hand.Count == 4 && g.State.Discard.Count == 0, "whole pile goes into the hand");

            var g2 = Table(3);
            Pile(g2, Rank.Five);
            Give(g2, 0, Rank.Nine);
            Check(!g2.PickUp(0).Ok, "can't pick up when a card is playable");
        }

        Section("Running out of time picks up the pile (multiplayer turn timer)");
        {
            var g = Table(3);
            Pile(g, Rank.Five, Rank.Six);
            Give(g, 0, Rank.Nine, Rank.Ten);
            var r = g.PickUpOnTimeout(0);
            Check(r.Ok && g.State.Players[0].Hand.Count == 4 && g.State.Discard.Count == 0, "the pile is taken even with playable cards");
            Check(r.Events[0].Type == GameEventType.TurnTimedOut, "the time-out is announced first");
            Check(g.State.Current == 1, "play passes on");
            Check(!g.PickUpOnTimeout(0).Ok, "only on your own turn");

            var g2 = Table(3);
            Give(g2, 0, Rank.Nine);
            Check(!g2.PickUpOnTimeout(0).Ok, "nothing to pick up: refused (a move is made for them instead)");
        }

        Section("Four of a kind in a row burns the pile (Q20)");
        {
            var g = Table(3);
            Pile(g, Rank.Three, Rank.Five, Rank.Five);
            var h = Give(g, 0, Rank.Five, Rank.Five, Rank.Nine);
            var r = g.PlayCards(0, PlaySource.Hand, Ids(h.Take(2)));
            Check(r.Ok && r.Events.Any(e => e.Type == GameEventType.PileBurned) && g.State.Discard.Count == 0, "two 5s on two 5s: four in a row burns");
            Check(g.State.Current == 0, "the player who completed the four goes again");

            var g2 = Table(3);
            Pile(g2, Rank.Six, Rank.Six, Rank.Six);
            var h2 = Give(g2, 0, Rank.Six, Rank.King);
            g2.PlayCards(0, PlaySource.Hand, Ids(h2.Take(1)));
            Check(g2.State.Discard.Count == 0 && g2.State.Current == 0, "a single 6 on three 6s (played by others) burns too");

            var g3 = Table(3);
            Pile(g3, Rank.Seven, Rank.Seven);
            var h3 = Give(g3, 0, Rank.Seven, Rank.Seven, Rank.Four);
            g3.PlayCards(0, PlaySource.Hand, Ids(h3.Take(2)));
            Check(g3.State.Phase == GamePhase.Playing && g3.State.Discard.Count == 0, "four 7s burn instead of asking HIGHER / LOWER");

            var g4 = Table(3);
            Pile(g4, Rank.Five, Rank.Nine, Rank.Nine);
            var h4 = Give(g4, 0, Rank.Nine, Rank.Four);
            g4.PlayCards(0, PlaySource.Hand, Ids(h4.Take(1)));
            Check(g4.State.Discard.Count == 4 && g4.State.Current == 1, "three in a row doesn't burn");

            var g5 = Table(3);
            g5.Rules.FourOfAKindBurns = false;
            Pile(g5, Rank.Five, Rank.Five, Rank.Five);
            var h5 = Give(g5, 0, Rank.Five, Rank.Four);
            g5.PlayCards(0, PlaySource.Hand, Ids(h5.Take(1)));
            Check(g5.State.Discard.Count == 4, "switch off: no burn");

            // The computers use it: they finish a four when they can.
            var g6 = Table(3);
            Pile(g6, Rank.Eight, Rank.Eight);
            Give(g6, 0, Rank.Eight, Rank.Eight, Rank.Four, Rank.Jack);
            var move = BotBrain.Decide(g6, 0, new Random(3), BotLevel.Normal);
            Check(move.Type == BotMoveType.PlayCards && move.CardIds.Count == 2 && BotBrain.Execute(g6, 0, move).Events.Any(e => e.Type == GameEventType.PileBurned),
                "a Normal computer completes four 8s to burn (instead of playing its cheaper card)");

            // Hard computers don't leave three of a kind on top for the next player to finish.
            var g7 = Table(3);
            Pile(g7, Rank.Four, Rank.Six, Rank.Six);
            Give(g7, 0, Rank.Six, Rank.Nine, Rank.Jack);
            var hard = BotBrain.Decide(g7, 0, new Random(9), BotLevel.Hard);
            int sixes = hard.CardIds.Count(id => g7.State.Players[0].Hand.First(c => c.Id == id).Rank == Rank.Six);
            Check(hard.Type == BotMoveType.PlayCards && sixes == 0, "a Hard computer avoids making three 6s in a row");
        }

        Section("Stuck on face-up cards: take one face-up card with the pile (Q11)");
        {
            var g = Table(3);
            var p = g.State.Players[0];
            Pile(g, Rank.Jack, Rank.King);
            p.FaceUp[0] = C(Rank.Nine); p.FaceUp[1] = C(Rank.Five); p.FaceUp[2] = C(Rank.Eight);
            Check(g.MustPickUp(0) && g.PickUpNeedsFaceUpChoice(0), "no playable face-up card: a choice is needed");
            Check(!g.PickUp(0).Ok, "picking up without choosing a face-up card is refused");
            Card five = p.FaceUp[1].Value;
            var r = g.PickUp(0, 1);
            Check(r.Ok && p.Hand.Count == 3 && p.Hand.Contains(five) && !p.FaceUp[1].HasValue && p.FaceUpCount == 2,
                "the chosen face-up card and the pile go into the hand");
            Check(r.Events[0].Type == GameEventType.FaceUpTaken && r.Events[1].Type == GameEventType.PileCollected, "events: card taken, then pile");
            Check(g.State.Current == 1 && p.ActiveSource == PlaySource.Hand, "turn passes; you're back to playing from your hand");

            var g2 = Table(3);
            g2.Rules.FaceUpPickUpTakesOne = false;
            var p2 = g2.State.Players[0];
            Pile(g2, Rank.King);
            p2.FaceUp[0] = C(Rank.Four);
            Check(g2.PickUp(0).Ok && p2.Hand.Count == 1 && p2.FaceUpCount == 1, "switch off: pile only, face-up card stays");

            var g3 = Table(3);
            Pile(g3, Rank.King);
            Give(g3, 0, Rank.Four);
            g3.State.Players[0].FaceUp[0] = C(Rank.Three);
            Check(!g3.PickUpNeedsFaceUpChoice(0) && g3.PickUp(0).Ok && g3.State.Players[0].FaceUpCount == 1, "from the hand: no face-up card is taken");
        }

        Section("Hand is topped back up to 3 while the deck lasts");
        {
            var g = Table(3);
            var hand = Give(g, 0, Rank.Five, Rank.Five, Rank.Six);
            g.State.DrawPile.AddRange(new[] { C(Rank.Nine), C(Rank.Jack), C(Rank.King) });
            g.PlayCards(0, PlaySource.Hand, Ids(hand.Take(2)));
            Check(g.State.Players[0].Hand.Count == 3 && g.State.DrawPile.Count == 1, "drew 2 replacements");
        }

        Section("Three stages: hand, then face-up, then face-down");
        {
            var g = Table(3);
            var p = g.State.Players[0];
            p.FaceUp[0] = C(Rank.Nine);
            Give(g, 0, Rank.Four);
            Check(!g.PlayCards(0, PlaySource.FaceUp, new List<int> { p.FaceUp[0].Value.Id }).Ok, "face-up blocked while hand has cards");
            p.Hand.Clear();
            Check(!g.PlayFaceDown(0, 0).Ok, "face-down blocked while face-up cards remain");
            Check(g.PlayCards(0, PlaySource.FaceUp, new List<int> { p.FaceUp[0].Value.Id }).Ok, "face-up allowed once the hand is empty");
        }

        Section("Face-up: several of the same rank at once");
        {
            var g = Table(3);
            var p = g.State.Players[0];
            p.FaceUp[0] = C(Rank.Jack); p.FaceUp[2] = C(Rank.Jack); p.FaceUp[1] = C(Rank.Four);
            var r = g.PlayCards(0, PlaySource.FaceUp, new List<int> { p.FaceUp[0].Value.Id, p.FaceUp[2].Value.Id });
            Check(r.Ok && p.FaceUpCount == 1, "both Jacks played from the table");
        }

        Section("Face-down: illegal card is collected with the pile");
        {
            var g = Table(3);
            var p = g.State.Players[0];
            p.FaceDown[0] = C(Rank.Four);
            p.FaceDown[1] = C(Rank.Queen);
            Pile(g, Rank.Six, Rank.King);
            var r = g.PlayFaceDown(0, 0);
            Check(r.Ok && p.Hand.Count == 3 && p.Hand.Any(c => c.Rank == Rank.Four), "hand = pile + revealed 4");
            Check(g.State.Discard.Count == 0 && g.State.Current == 1, "pile empty, turn passes");
            Check(p.ActiveSource == PlaySource.Hand, "must clear the collected hand before more face-down cards");
        }

        Section("Face-down: legal card is played");
        {
            var g = Table(3);
            var p = g.State.Players[0];
            p.FaceDown[0] = C(Rank.Queen);
            p.FaceDown[1] = C(Rank.Three);
            Pile(g, Rank.Five);
            var r = g.PlayFaceDown(0, 0);
            Check(r.Ok && g.State.TopRank == Rank.Queen && p.Hand.Count == 0, "revealed Queen is played");
        }

        Section("Finishing order and the game continuing");
        {
            var g = Table(3);
            var p0 = g.State.Players[0];
            p0.FaceDown[0] = null;
            var hand = Give(g, 0, Rank.Nine);
            var r = g.PlayCards(0, PlaySource.Hand, Ids(hand));
            Check(p0.FinishPosition == 1 && g.State.Phase == GamePhase.Playing, "first out is 1st and the game carries on");
            Check(g.State.Current == 1, "next player continues");

            var p1 = g.State.Players[1];
            p1.FaceDown[0] = null;
            var h1 = Give(g, 1, Rank.King);
            r = g.PlayCards(1, PlaySource.Hand, Ids(h1));
            Check(p1.FinishPosition == 2, "second out is 2nd");
            Check(g.State.Phase == GamePhase.GameOver && g.State.Players[2].FinishPosition == 3, "last player is 3rd and the game ends");
            Check(r.Events.Any(e => e.Type == GameEventType.GameOver), "GameOver event sent");
        }

        Section("Finished players are skipped in turn order");
        {
            var g = Table(4);
            g.State.Players[1].FaceDown[0] = null;
            g.State.Players[1].FinishPosition = 1;
            g.State.FinishOrder.Add(1);
            var hand = Give(g, 0, Rank.Five);
            g.PlayCards(0, PlaySource.Hand, Ids(hand));
            Check(g.State.Current == 2, "player 2 (finished) is skipped");
        }

        Section("Finishing on a 10 passes the turn on");
        {
            var g = Table(3);
            g.State.Players[0].FaceDown[0] = null;
            var hand = Give(g, 0, Rank.Ten);
            Pile(g, Rank.Six);
            g.PlayCards(0, PlaySource.Hand, Ids(hand));
            Check(g.State.Current == 1 && g.State.Discard.Count == 0, "next player starts on an empty pile");
        }
    }

    // ------------------------------------------------------------------ network messages

    private static void NetworkMessages()
    {
        Console.WriteLine();
        Console.WriteLine("Network messages");
        var rng = new Random(99);
        bool privacyOk = true, roundTripOk = true, legalityOk = true, eventsOk = true;
        int updates = 0;

        for (int g = 0; g < 60; g++)
        {
            int players = 2 + g % 9;
            var game = PalaceGame.Create(Enumerable.Range(0, players).Select(i => "P" + i).ToList(),
                Enumerable.Repeat(true, players).ToList(), new HouseRules(), rng.Next());
            var events = game.StartEvents.ToList();

            for (int step = 0; step < 400 && game.State.Phase != GamePhase.GameOver; step++)
            {
                int viewer = step % players;
                var (v, evs, view) = Decode(Palace.Net.NetProtocol.Update(game.State, events, viewer));
                updates++;
                var s = game.State;

                // Privacy: only the viewer's own hand is real; face-down cards and the deck never are.
                for (int p = 0; p < players; p++)
                {
                    var real = s.Players[p];
                    var seen = view.Players[p];
                    if (seen.Hand.Count != real.Hand.Count) roundTripOk = false;
                    if (p == viewer && !seen.Hand.Select(c => c.Id).SequenceEqual(real.Hand.Select(c => c.Id))) roundTripOk = false;
                    if (p != viewer && seen.Hand.Any(c => c.Id >= 0)) privacyOk = false;
                    if (seen.FaceDown.Any(c => c.HasValue && c.Value.Id >= 0)) privacyOk = false;
                    if (seen.FaceDownCount != real.FaceDownCount || seen.FinishPosition != real.FinishPosition) roundTripOk = false;
                    for (int k = 0; k < 3; k++) if (!Equals(seen.FaceUp[k], real.FaceUp[k])) roundTripOk = false;
                }
                if (view.DrawPile.Any(c => c.Id >= 0)) privacyOk = false;
                if (view.DrawPile.Count != s.DrawPile.Count || !view.Discard.SequenceEqual(s.Discard)) roundTripOk = false;
                if (view.Current != s.Current || view.Phase != s.Phase || view.Direction != s.Direction || view.PendingPlayer != s.PendingPlayer
                    || view.AceValue != s.AceValue || view.ActiveSeven != s.ActiveSeven || !view.PendingAceValues.SequenceEqual(s.PendingAceValues))
                    roundTripOk = false;

                // The viewer's device must reach the same rule decisions as the host.
                var mirror = new PalaceGame(view, new HouseRules(), 0);
                for (int r = 2; r <= 14; r++)
                    if (mirror.CheckRank((Rank)r).Legal != game.CheckRank((Rank)r).Legal) legalityOk = false;
                if (mirror.MustPickUp(viewer) != game.MustPickUp(viewer)) legalityOk = false;

                foreach (var e in evs.Where(e => e.Type == GameEventType.CardsDrawn && e.Player != viewer))
                    if (e.Cards.Any(c => c.Id >= 0)) eventsOk = false;

                int actor = s.ActingPlayer;
                var move = BotBrain.Decide(game, actor, rng);
                var result = BotBrain.Execute(game, actor, move);
                events = result.Events;
            }
        }

        var cmd = new Palace.Net.NetCommand { Type = Palace.Net.NetCommandType.PlayCards, Source = PlaySource.FaceUp, Slot = 2, AceValue = Rank.Queen };
        cmd.CardIds.AddRange(new[] { 5, 17 });
        byte[] bytes = Palace.Net.NetProtocol.Command(cmd);
        var back = Palace.Net.NetProtocol.ReadCommand(new BinaryReader(new MemoryStream(bytes, 1, bytes.Length - 1)));
        Check(back.Type == cmd.Type && back.Source == cmd.Source && back.CardIds.SequenceEqual(cmd.CardIds) && back.AceValue == Rank.Queen,
            "a move survives the trip to the host unchanged");

        Check(roundTripOk, "every player's view of the table arrives intact (" + updates + " updates checked)");
        Check(privacyOk, "no other player's hand, face-down card or deck order is ever sent");
        Check(eventsOk, "cards drawn by other players stay hidden");
        Check(legalityOk, "each device works out the same legal moves as the host");
    }

    private static (int viewer, List<GameEvent> events, GameState state) Decode(byte[] data)
    {
        var r = new BinaryReader(new MemoryStream(data));
        r.ReadByte(); // message type
        return Palace.Net.NetProtocol.ReadUpdate(r);
    }

    // ------------------------------------------------------------------ part 2

    private static void Simulations()
    {
        Console.WriteLine();
        Console.WriteLine("Simulated bot games");

        var rng = new Random(12345);
        int games = 0, longest = 0;
        bool conservationOk = true, positionsOk = true, finishedOk = true, noErrors = true;
        var turnsByCount = new Dictionary<int, List<int>>();

        for (int players = 2; players <= 10; players++)
        {
            turnsByCount[players] = new List<int>();
            for (int k = 0; k < 400; k++)
            {
                var names = Enumerable.Range(0, players).Select(i => "Bot" + i).ToList();
                var bots = Enumerable.Repeat(true, players).ToList();
                var game = PalaceGame.Create(names, bots, new HouseRules(), rng.Next());
                int expectedTotal = game.Rules.DecksFor(players) * 52;
                if (game.State.TotalCards != expectedTotal) conservationOk = false;

                int actions = 0;
                while (game.State.Phase != GamePhase.GameOver && actions < 20000)
                {
                    int actor = game.State.ActingPlayer;
                    var move = BotBrain.Decide(game, actor, rng);
                    var result = BotBrain.Execute(game, actor, move);
                    if (!result.Ok)
                    {
                        if (noErrors) Console.WriteLine($"  first error ({players}p): {result.Error} for {move.Type}");
                        noErrors = false;
                        break;
                    }
                    if (CountCards(game.State) != game.State.TotalCards) conservationOk = false;
                    actions++;
                }

                games++;
                longest = Math.Max(longest, actions);
                turnsByCount[players].Add(actions);
                if (game.State.Phase != GamePhase.GameOver) finishedOk = false;

                var positions = game.State.Players.Select(p => p.FinishPosition).OrderBy(x => x).ToList();
                if (!positions.SequenceEqual(Enumerable.Range(1, players))) positionsOk = false;
            }
        }

        Check(noErrors, "bots never attempt an illegal move");
        Check(finishedOk, "every game ends");
        Check(conservationOk, "no card is ever created or lost (checked after every action)");
        Check(positionsOk, "finishing positions are always 1..N with no gaps or repeats");

        foreach (var kv in turnsByCount)
            Console.WriteLine($"  {kv.Key,2} players: 400 games, average {kv.Value.Average():F0} actions, max {kv.Value.Max()}");
        Console.WriteLine($"  {games} games total, longest {longest} actions");
    }

    // ------------------------------------------------------------------ part 3

    private static void BotLevelsAndTutorial()
    {
        Console.WriteLine();
        Console.WriteLine("Bot skill levels and the guided game");

        // Head-to-head: seat 0 plays at one level, everyone else at another. The first player is
        // random, so the only difference is skill. A game still going after 5000 moves is counted as
        // a stalemate (see the 2-player 8s note in Docs/PROGRESS.md).
        bool allLegal = true;
        int stalemates = 0, played = 0;
        double Rate(BotLevel a, BotLevel b, int players, int games)
        {
            var rng = new Random(777 + players * 13 + (int)a * 5 + (int)b);
            int wins = 0;
            for (int k = 0; k < games; k++)
            {
                var names = Enumerable.Range(0, players).Select(i => "Bot" + i).ToList();
                var game = PalaceGame.Create(names, Enumerable.Repeat(true, players).ToList(), new HouseRules(), rng.Next());
                int actions = 0;
                while (game.State.Phase != GamePhase.GameOver && actions < 5000)
                {
                    int actor = game.State.ActingPlayer;
                    var res = BotBrain.Execute(game, actor, BotBrain.Decide(game, actor, rng, actor == 0 ? a : b));
                    if (!res.Ok) { allLegal = false; break; }
                    actions++;
                }
                played++;
                if (game.State.Phase != GamePhase.GameOver) stalemates++;
                else if (game.State.FinishOrder[0] == 0) wins++;
            }
            return wins / (double)games;
        }

        double hardVsEasy = Rate(BotLevel.Hard, BotLevel.Easy, 2, 3000);
        double hardVsNormal = Rate(BotLevel.Hard, BotLevel.Normal, 2, 3000);
        double normalVsEasy = Rate(BotLevel.Normal, BotLevel.Easy, 2, 3000);
        double hardVsNormal4 = Rate(BotLevel.Hard, BotLevel.Normal, 4, 3000);
        double easyVsHard6 = Rate(BotLevel.Easy, BotLevel.Hard, 6, 1000);
        Console.WriteLine($"  1 v 1 win rate: Hard v Easy {hardVsEasy:P0}, Hard v Normal {hardVsNormal:P0}, Normal v Easy {normalVsEasy:P0}");
        Console.WriteLine($"  4 players, Hard against 3 Normal: Hard wins {hardVsNormal4:P0} (fair share 25%)");
        Console.WriteLine($"  6 players, Easy against 5 Hard: Easy wins {easyVsHard6:P0} (fair share 17%)");
        Console.WriteLine($"  stalemates: {stalemates} of {played} games");
        Check(allLegal, "every skill level only makes legal moves");
        Check(stalemates == 0, "no game ever gets stuck (the 2-player 8s loop is gone)");
        Check(hardVsEasy > 0.6, "Hard beats Easy most of the time");
        Check(normalVsEasy > 0.55, "Normal beats Easy more often than not");
        Check(hardVsNormal > 0.53, "Hard beats Normal more often than not");
        Check(hardVsNormal4 > 0.28, "Hard wins clearly more than its fair share at a 4-player table");

        // The guided game: a stacked deck, otherwise normal.
        var t = TutorialSetup.Create("You", new HouseRules(), 99);
        var you = t.State.Players[0];
        Check(t.State.Current == 0, "guided game: you go first");
        Check(you.Hand.Select(c => c.Rank).OrderBy(r => r).SequenceEqual(new[] { Rank.Four, Rank.Four, Rank.Seven }), "guided game: you hold 4, 4, 7");
        Check(you.FaceUp.Count(c => c.HasValue && c.Value.Rank == Rank.King) == 2, "guided game: two Kings face up (to learn playing a pair)");
        Check(t.State.DrawPile[t.State.DrawPile.Count - 1].Rank == Rank.Ten, "guided game: the first card drawn is a 10");
        Check(CountCards(t.State) == 52 && t.State.DrawPile.Select(c => c.Id).Concat(t.State.Players.SelectMany(p => p.Hand.Select(c => c.Id))).Distinct().Count() == 52 - 27 + 9,
            "guided game: one normal 52-card deck");
        var trng = new Random(5);
        bool tutorialEnds = true;
        for (int k = 0; k < 300; k++)
        {
            var g = TutorialSetup.Create("You", new HouseRules(), k);
            int actions = 0;
            while (g.State.Phase != GamePhase.GameOver && actions < 20000)
            {
                int actor = g.State.ActingPlayer;
                if (!BotBrain.Execute(g, actor, BotBrain.Decide(g, actor, trng, actor == 0 ? BotLevel.Normal : BotLevel.Easy)).Ok) break;
                actions++;
            }
            if (g.State.Phase != GamePhase.GameOver) tutorialEnds = false;
        }
        Check(tutorialEnds, "guided game: 300 played-out games all end normally");
    }

    // ------------------------------------------------------------------ part 4

    private static void SwapBeforePlay()
    {
        Console.WriteLine();
        Console.WriteLine("Optional mode: swap before play");

        var names = new List<string> { "A", "B", "C" };
        var bots = new List<bool> { false, true, true };
        var g = PalaceGame.Create(names, bots, new HouseRules { SwapBeforePlay = true }, 42);
        var s = g.State;
        Check(s.Phase == GamePhase.Swapping, "the game starts in the swap phase");
        Check(!g.StartEvents.Any(e => e.Type == GameEventType.TurnChanged), "no first turn until everyone is ready");
        var me = s.Players[0];
        Check(!g.PlayCards(s.Current, PlaySource.Hand, new List<int> { s.Players[s.Current].Hand[0].Id }).Ok, "nobody can play while swapping");

        Card hand0 = me.Hand[0], up1 = me.FaceUp[1].Value;
        var r = g.SwapCards(0, hand0.Id, 1);
        Check(r.Ok && me.FaceUp[1].Value.Id == hand0.Id && me.Hand.Any(c => c.Id == up1.Id), "a hand card and a face-up card change places");
        Check(r.Events.Count == 1 && r.Events[0].Type == GameEventType.CardsSwapped, "the swap is reported as one event");
        Check(!g.SwapCards(0, 99999, 0).Ok, "a card you don't hold can't be swapped");
        Check(!g.SwapCards(0, me.Hand[0].Id, 5).Ok, "a face-up slot that doesn't exist is refused");
        Check(me.Hand.Count == 3 && me.FaceUpCount == 3 && CountCards(s) == 52, "no card is created or lost by swapping");

        Check(g.FinishSwap(0).Ok && !g.FinishSwap(0).Ok, "Ready can only be pressed once");
        Check(!g.SwapCards(0, me.Hand[0].Id, 0).Ok, "no more swaps after pressing Ready");
        Check(s.Phase == GamePhase.Swapping, "still swapping while others aren't ready");
        g.FinishSwap(1);
        var last = g.FinishSwap(2);
        Check(s.Phase == GamePhase.Playing && last.Events.Any(e => e.Type == GameEventType.TurnChanged && e.Player == s.Current),
            "when the last player is ready, the first turn begins");

        // Bots put their best cards face up.
        var p = new PlayerState(0, "Bot", true);
        p.Hand.AddRange(new[] { C(Rank.Ten), C(Rank.Ace), C(Rank.Three) });
        p.FaceUp[0] = C(Rank.Four); p.FaceUp[1] = C(Rank.King); p.FaceUp[2] = C(Rank.Five);
        var swaps = BotBrain.DecideSwaps(p, BotLevel.Normal, new Random(1));
        foreach (var (id, slot) in swaps)
        {
            int i = p.Hand.FindIndex(c => c.Id == id);
            Card t = p.Hand[i]; p.Hand[i] = p.FaceUp[slot].Value; p.FaceUp[slot] = t;
        }
        var upRanks = p.FaceUp.Select(c => c.Value.Rank).OrderBy(x => x).ToList();
        Check(upRanks.SequenceEqual(new[] { Rank.Ten, Rank.King, Rank.Ace }), "a bot moves its 10 and Ace face up and keeps the low cards");

        // Full games in swap mode, every level, many table sizes.
        var rng = new Random(2024);
        bool allEnd = true, legal = true, conserved = true;
        for (int k = 0; k < 600; k++)
        {
            int n = 2 + k % 9;
            var game = PalaceGame.Create(Enumerable.Range(0, n).Select(i => "P" + i).ToList(), Enumerable.Repeat(true, n).ToList(),
                new HouseRules { SwapBeforePlay = true }, rng.Next());
            BotLevel level = (BotLevel)(k % 3);
            foreach (var pl in game.State.Players)
            {
                foreach (var (id, slot) in BotBrain.DecideSwaps(pl, level, rng))
                    if (!game.SwapCards(pl.Index, id, slot).Ok) legal = false;
                if (!game.FinishSwap(pl.Index).Ok) legal = false;
            }
            if (CountCards(game.State) != game.State.TotalCards) conserved = false;
            int actions = 0;
            while (game.State.Phase != GamePhase.GameOver && actions < 5000)
            {
                int actor = game.State.ActingPlayer;
                if (!BotBrain.Execute(game, actor, BotBrain.Decide(game, actor, rng, level)).Ok) { legal = false; break; }
                actions++;
            }
            if (game.State.Phase != GamePhase.GameOver) allEnd = false;
        }
        Check(legal, "swap mode: bots only make legal swaps and moves");
        Check(conserved, "swap mode: every card is still there after swapping");
        Check(allEnd, "swap mode: 600 games at every level and table size all end");

        // Online: the swap phase and who is ready travel to every device; swap commands reach the host intact.
        var g2 = PalaceGame.Create(names, bots, new HouseRules { SwapBeforePlay = true }, 7);
        g2.FinishSwap(1);
        byte[] bytes = Palace.Net.NetProtocol.Update(g2.State, new List<GameEvent>(), 0);
        var (_, _, view) = Decode(bytes);
        Check(view.Phase == GamePhase.Swapping && view.Players[1].SwapReady && !view.Players[0].SwapReady, "the swap phase and Ready flags reach other devices");
        var mirror = new PalaceGame(view, new HouseRules(), 0);
        var cmd = new Palace.Net.NetCommand { Type = Palace.Net.NetCommandType.Swap, Slot = 2 };
        cmd.CardIds.Add(view.Players[0].Hand[0].Id);
        byte[] cb = Palace.Net.NetProtocol.Command(cmd);
        var back = Palace.Net.NetProtocol.ReadCommand(new BinaryReader(new MemoryStream(cb, 1, cb.Length - 1)));
        Check(back.ApplyTo(g2, 0).Ok && g2.State.Players[0].FaceUp[2].Value.Id == cmd.CardIds[0], "a swap sent from a phone is applied by the host");
        Check(mirror.State.Phase == GamePhase.Swapping, "a phone's copy of the game knows it's the swap phase");

        // Online: each player's picture and cosmetics reach everyone in the game room.
        var seats = new List<Palace.Net.LobbySeat>
        {
            new Palace.Net.LobbySeat { ClientId = 0, Name = "Host", Ready = true, Avatar = 4, Banner = "banner_flag_poland", NameStyle = "name_neon",
                Font = "font_bangers", Effect = "fx_galaxy", Celebration = "win_fireworks" },
            new Palace.Net.LobbySeat { Name = "Bot", IsBot = true, Ready = true }
        };
        byte[] lobby = Palace.Net.NetProtocol.Lobby(seats);
        var got = Palace.Net.NetProtocol.ReadLobby(new BinaryReader(new MemoryStream(lobby, 1, lobby.Length - 1)));
        Check(got.Count == 2 && got[0].Avatar == 4 && got[0].Banner == "banner_flag_poland" && got[0].NameStyle == "name_neon" && got[0].Font == "font_bangers"
              && got[0].Effect == "fx_galaxy" && got[0].Celebration == "win_fireworks" && got[1].IsBot && got[1].Avatar == -1 && got[1].Effect == "",
            "pictures, banners, name styles, fonts, effects and celebrations reach every device");
    }

    // ------------------------------------------------------------------ game modes

    private static void GameModes()
    {
        Console.WriteLine();
        Console.WriteLine("Game modes: decks and cards in hand");

        var classic = new HouseRules();
        Check(classic.DecksFor(2) == 1 && classic.DecksFor(5) == 1 && classic.DecksFor(6) == 2 && classic.DecksFor(10) == 2,
            "Classic: one deck, two from 6 players (unchanged)");
        Check(new HouseRules { DeckCount = 2 }.DecksFor(4) == 2 && new HouseRules { DeckCount = 3 }.DecksFor(2) == 3,
            "a chosen number of decks is used (e.g. a 4-player game with 2 decks)");
        Check(new HouseRules { DeckCount = 1 }.DecksFor(10) == 2, "if one deck can't deal 10 players, a second is added");
        Check(new HouseRules { HandCards = 5 }.DecksFor(10) == 3, "10 players with 5 cards in hand get 3 decks so the deal fits");

        var names = new List<string> { "A", "B", "C", "D" };
        var bots = Enumerable.Repeat(true, 4).ToList();
        var g = PalaceGame.Create(names, bots, new HouseRules { DeckCount = 2, HandCards = 5 }, 99);
        Check(g.State.TotalCards == 104 && g.State.Players.All(p => p.Hand.Count == 5 && p.FaceDownCount == 3 && p.FaceUpCount == 3),
            "a 2-deck, 5-card game deals 104 cards and 5 to each hand");
        Check(g.State.DrawPile.Count == 104 - 4 * 11, "the rest of both decks is the draw pile");
        Check(g.State.Players.Select(p => p.Hand.Concat(p.FaceUp.Where(c => c.HasValue).Select(c => c.Value))).SelectMany(x => x)
                .Select(c => c.Id).Distinct().Count() == 4 * 8, "every card has its own id, even with two decks");

        // After a play the hand is topped back up to the mode's size.
        int actor = g.State.Current;
        var move = BotBrain.Decide(g, actor, new Random(3));
        BotBrain.Execute(g, actor, move);
        Check(g.State.Players[actor].Hand.Count >= 5 || g.State.DrawPile.Count == 0, "the hand is refilled to 5 cards while the deck lasts");

        // Full games in every mode mix: always legal, nothing lost, every game ends.
        var rng = new Random(5150);
        bool legal = true, conserved = true, ended = true, handsOk = true;
        int played = 0;
        for (int k = 0; k < 720; k++)
        {
            int n = 2 + k % 9;
            var rules = new HouseRules
            {
                DeckCount = k % 4,
                HandCards = 3 + (k / 4) % 3,
                FourOfAKindBurns = (k / 12) % 2 == 0,
                SwapBeforePlay = (k / 24) % 2 == 0
            };
            var game = PalaceGame.Create(Enumerable.Range(0, n).Select(i => "P" + i).ToList(), Enumerable.Repeat(true, n).ToList(), rules, rng.Next());
            if (game.State.TotalCards != rules.DecksFor(n) * 52) conserved = false;
            if (game.State.Players.Any(p => p.Hand.Count != rules.HandCards)) handsOk = false;
            BotLevel level = (BotLevel)(k % 3);
            if (rules.SwapBeforePlay)
                foreach (var pl in game.State.Players)
                {
                    foreach (var (id, slot) in BotBrain.DecideSwaps(pl, level, rng))
                        if (!game.SwapCards(pl.Index, id, slot).Ok) legal = false;
                    if (!game.FinishSwap(pl.Index).Ok) legal = false;
                }
            int actions = 0;
            while (game.State.Phase != GamePhase.GameOver && actions < 8000)
            {
                int a = game.State.ActingPlayer;
                if (!BotBrain.Execute(game, a, BotBrain.Decide(game, a, rng, level)).Ok) { legal = false; break; }
                if (CountCards(game.State) != game.State.TotalCards) conserved = false;
                actions++;
            }
            if (game.State.Phase != GamePhase.GameOver) ended = false;
            played++;
        }
        Check(handsOk, "every mode deals the chosen number of cards to each hand");
        Check(legal, "every mode: bots only make legal moves");
        Check(conserved, "every mode: no card is created or lost, with 1, 2 or 3 decks");
        Check(ended, $"every mode: {played} games (1-3 decks, 3-5 cards, burns on/off, swap on/off) all end");

        // Online: the host's mode reaches everyone in the room; an older host (no mode) means Classic.
        var seats = new List<Palace.Net.LobbySeat> { new Palace.Net.LobbySeat { Name = "Host", Ready = true } };
        byte[] lobby = Palace.Net.NetProtocol.Lobby(seats, new HouseRules { DeckCount = 2, HandCards = 4, FourOfAKindBurns = false, SwapBeforePlay = true });
        var r = new BinaryReader(new MemoryStream(lobby, 1, lobby.Length - 1));
        Palace.Net.NetProtocol.ReadLobby(r);
        var mode = Palace.Net.NetProtocol.ReadMode(r);
        Check(mode != null && mode.DeckCount == 2 && mode.HandCards == 4 && !mode.FourOfAKindBurns && mode.SwapBeforePlay,
            "the host's game mode reaches every phone in the room");
        var old = new BinaryReader(new MemoryStream(lobby, 1, lobby.Length - 1 - 5));
        Palace.Net.NetProtocol.ReadLobby(old);
        Check(Palace.Net.NetProtocol.ReadMode(old) == null, "a room from an older version (no mode sent) is treated as Classic");
    }

    private static int CountCards(GameState s)
    {
        int n = s.DrawPile.Count + s.Discard.Count + s.BurnedCount;
        foreach (var p in s.Players) n += p.TotalCards;
        return n;
    }
}
