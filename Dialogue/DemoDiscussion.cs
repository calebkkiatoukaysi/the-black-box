namespace TheBlackBox;

/// <summary>
/// The stand-in opponent, so the discussion period can be played before anybody is written.
/// </summary>
/// <remarks>
/// Placeholder, meant to be thrown away, and it says nothing about the lore on purpose since
/// that is still being written. What it does do is use every feature: all three tempers, a
/// locked option, a flag, endings and non-endings, and a conversation that moves on each round.
/// </remarks>
public static class DemoDiscussion
{
    /// <summary>How long the box allows each discussion. Generous, because it is the one people learn on.</summary>
    /// <remarks>Was 45 before lines were held for a beat. That costs about ten seconds a discussion, so this is 45 plus that.</remarks>
    public const float Seconds = 56f;

    /// <summary>Less shut than neutral, or four beats would never be enough to unlock the locked question. A longer script should start nearer 50.</summary>
    public static readonly Disposition Opening = new(0, 34);

    /// <summary>The stand-in opponent's script.</summary>
    public static readonly DialogueScript Script = new()
    {
        OpponentId = "demo",
        OpponentName = "THE OPPONENT",
        Openings = new[] { "open", "second", "third", "fourth", "late" },
        Seconds = Seconds,
        Opens = Opening,
        Nodes = new DialogueNode[]
        {
            // ---- round one: sitting down ------------------------------------------------

            new()
            {
                Id = "open",
                Even = "You are new. I can tell, because you are still looking at the box instead of at me.",
                Hostile = "Sit. Do not talk to me more than the night requires.",
                Open = "You are new. Sit down. It is easier if you look at me and not at it.",
                Options = new DialogueOption[]
                {
                    new("GOOD TO MEET YOU", "Then I will look at you. I am {name}.",
                        Tone.Warm, Next: "name"),

                    new("FIRST NIGHT", "First night. I do not know what I am supposed to do yet.",
                        Tone.Level, Next: "rules"),

                    new("HOW LONG?", "How long have you been sitting at this table?",
                        Tone.Probing, Next: "howlong"),

                    new("MIND YOURSELF", "Worry about your own hands.",
                        Tone.Cutting, Next: "rules"),
                },
            },

            new()
            {
                Id = "name",
                Even = "{name}. All right. Names do not last long in this room, but it is good to hear one.",
                Hostile = "Keep it. I have watched a lot of names go into that box.",
                Open = "{name}. I will try to remember it. That is not nothing, in here.",
                Options = new DialogueOption[]
                {
                    // Carries most of the guard reduction on purpose. This is what opens HOW MANY? later.
                    new("AND YOURS?", "And what should I call you?",
                        Tone.Warm, Next: "howlong", Value: 3, Guard: -8),

                    new("IT'S JUST A NAME", "It is only a name. It does not cost either of us anything.",
                        Tone.Level, Next: "rules"),

                    new("WHY WOULDN'T IT?", "Why would a name not last?",
                        Tone.Probing, Next: "howlong", Flag: "demo.asked.names"),

                    new("SPARE ME", "Do not get sentimental. One of us is leaving without a hand.",
                        Tone.Cutting, Next: "rules"),
                },
            },

            new()
            {
                Id = "rules",
                Even = "We talk until it decides we are finished. Then we both feed it, and it gives us both something, and we find out what you are.",
                Hostile = "You talk. It listens. Then it takes a hand off each of us. That is all you need.",
                Open = "We talk while it lets us. Then we feed it together, and it pays us both. Whatever it puts in your hand -- do not let me see your face when you look at it.",
                Options = new DialogueOption[]
                {
                    new("THANK YOU", "Thank you. You did not have to tell me that.",
                        Tone.Warm, Next: "close", Value: 4),

                    new("UNDERSTOOD", "Understood.",
                        Tone.Level, Next: "close"),

                    new("WHAT IS IT?", "What is it, actually? Underneath.",
                        Tone.Probing, Next: "close", Guard: 6, Flag: "demo.asked.box"),

                    new("I'LL MANAGE", "I will work it out without you.",
                        Tone.Cutting, Next: "close"),
                },
            },

            new()
            {
                Id = "howlong",
                Even = "Long enough that I have stopped counting the nights, and not so long that I have stopped counting the people.",
                Hostile = "Longer than you will.",
                Open = "Long enough. I have watched people sit exactly where you are sitting and I have been the reason some of them did not get up.",
                Options = new DialogueOption[]
                {
                    new("I'M SORRY", "I am sorry. That is a thing to carry.",
                        Tone.Warm, Next: "close", Value: 5),

                    new("THEN YOU'RE GOOD", "Then you are good at this, and I am not.",
                        Tone.Level, Next: "close"),

                    new("HOW MANY?", "How many of them were there?",
                        Tone.Probing, Next: "close", Guard: 8, Flag: "demo.asked.count",
                        RequiresCandid: true),

                    new("GOOD FOR YOU", "And you are still here telling me about it. Good for you.",
                        Tone.Cutting, Next: "close"),
                },
            },

            new()
            {
                Id = "close",
                Even = "It has heard enough of us. Put your hand where it can reach.",
                Hostile = "Enough. Hand. Now.",
                Open = "It has heard enough. Put your hand out -- and {name}, whatever it gives you, do not use it on the first round. Nobody does.",
                Options = new DialogueOption[]
                {
                    new("GOOD LUCK", "Good luck to you.", Tone.Warm, Value: 3),
                    new("READY", "I am ready.", Tone.Level),
                    new("YOU FIRST", "You put yours out first.", Tone.Probing),
                    new("GET ON WITH IT", "Get on with it.", Tone.Cutting),
                },
            },

            // ---- round two: after the first hand ----------------------------------------

            new()
            {
                Id = "second",
                Even = "So. Now you know what it feels like when it takes hold. Did it give you anything worth having?",
                Hostile = "Still here. Whatever it gave you, keep your face off it. I am not interested.",
                Open = "You did not flinch. Most people do, the first time. Whatever it gave you, {name} -- do not tell me. It is worse for both of us if I know.",
                Options = new DialogueOption[]
                {
                    new("IT WAS STRANGE", "It was strange. I kept my hand in there longer than I meant to.",
                        Tone.Warm, Next: "second.hand", Value: 2),

                    new("I'M FINE", "I am fine. It is only a hand.",
                        Tone.Level, Next: "second.hand"),

                    new("WHAT DID YOU GET?", "What did it give you?",
                        Tone.Probing, Next: "second.hand", Guard: 8, Flag: "demo.asked.hand"),

                    new("NOT YOUR BUSINESS", "You will find out what it gave me when I use it.",
                        Tone.Cutting, Next: "second.hand"),
                },
            },

            new()
            {
                Id = "second.hand",
                Even = "It likes people who ask. It likes them less the second time. Hand out.",
                Hostile = "Hand out. I would like this over with.",
                Open = "Hand out, {name}. And keep something back this time. Your pocket is the one part of this it does not watch.",
                Options = new DialogueOption[]
                {
                    new("AFTER YOU", "After you.", Tone.Warm, Value: 2),
                    new("READY", "Ready.", Tone.Level),
                    new("DOES IT WATCH?", "Does it watch what we keep?", Tone.Probing, Guard: 5, Flag: "demo.asked.pockets"),
                    new("STOP TALKING", "Stop talking and put your hand in.", Tone.Cutting),
                },
            },

            // ---- round three: counting ------------------------------------------------

            new()
            {
                Id = "third",
                Even = "Three hands in. This is where people start counting what they have got against what I have got. Do not. It never adds up the way you think.",
                Hostile = "You are still counting on your fingers. Good. It means you have not learned anything.",
                Open = "Three in. This is usually where I stop liking whoever is across from me. I have not stopped yet.",
                Options = new DialogueOption[]
                {
                    new("I'M NOT COUNTING", "I am not counting. I am watching you.",
                        Tone.Warm, Next: "third.hand", Value: 3, Guard: -3),

                    new("IT ADDS UP", "It adds up fine. Three each, a few things in a pocket, one box.",
                        Tone.Level, Next: "third.hand"),

                    new("WHAT ARE YOU HOLDING?", "What are you carrying?",
                        Tone.Probing, Next: "third.hand", Guard: 9, Flag: "demo.asked.carrying"),

                    new("YOU'RE SCARED", "You are scared. I can hear it.",
                        Tone.Cutting, Next: "third.hand", Value: -2),
                },
            },

            new()
            {
                Id = "third.hand",
                Even = "We will see. Hand.",
                Hostile = "Hand. And keep your eyes on it, not on me.",
                Open = "We will see. Hand out, {name}. I will try not to make it worse than it has to be.",
                Options = new DialogueOption[]
                {
                    new("SAME TO YOU", "Same to you.", Tone.Warm, Value: 2),
                    new("HAND", "Hand.", Tone.Level),
                    new("WHY WOULD YOU?", "Why would you make it worse?", Tone.Probing, Guard: 4),
                    new("TRY HARDER", "Try harder than that.", Tone.Cutting),
                },
            },

            // ---- round four: the quiet before something lands ---------------------------

            new()
            {
                Id = "fourth",
                Even = "It has been quiet. That is not the same as it being finished with us.",
                Hostile = "Something is going to happen to one of us this hand. I would rather it was you.",
                Open = "Look at me, {name}. Not at the box. Whatever it does next, I want you to have looked at me first.",
                Options = new DialogueOption[]
                {
                    new("I'M LOOKING", "I am looking.",
                        Tone.Warm, Next: "fourth.hand", Value: 4),

                    new("IT'S NEVER FINISHED", "It is never finished. That is the only rule I have worked out.",
                        Tone.Level, Next: "fourth.hand"),

                    // Only for an opponent who has been talked open across three rounds.
                    new("HOW DOES IT END?", "How does this end? For the one who gets up.",
                        Tone.Probing, Next: "fourth.hand", Guard: 8, Flag: "demo.asked.end",
                        RequiresCandid: true),

                    new("SAVE IT", "Save it. We both know what happens next.",
                        Tone.Cutting, Next: "fourth.hand"),
                },
            },

            new()
            {
                Id = "fourth.hand",
                Even = "Hand.",
                Hostile = "Hand. Now.",
                Open = "Hand out. We will talk after, if there is an after.",
                Options = new DialogueOption[]
                {
                    new("AFTER, THEN", "After, then.", Tone.Warm, Value: 2),
                    new("HAND", "Hand.", Tone.Level),
                    new("WILL THERE BE?", "Will there be an after?", Tone.Probing, Guard: 4),
                    new("JUST DO IT", "Just put it in.", Tone.Cutting),
                },
            },

            // ---- every round after: running out of things to say -------------------------

            new()
            {
                Id = "late",
                Even = "There is not much left to say that the box has not already heard.",
                Hostile = "I have nothing to say to you.",
                Open = "We are still here, {name}. I did not think we would be.",
                Options = new DialogueOption[]
                {
                    new("STILL HERE", "Still here.", Tone.Warm, Value: 2),
                    new("HAND OUT", "Hand out.", Tone.Level),
                    new("ARE YOU TIRED?", "Are you tired?", Tone.Probing, Guard: 4),
                    new("FINISH IT", "Then let us finish it.", Tone.Cutting),
                },
            },
        },
    };
}
