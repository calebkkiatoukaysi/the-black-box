namespace TheBlackBox;

/// <summary>
/// Serenity: the first person across the table. Polite, shy, and worn down by the games.
/// </summary>
/// <remarks>
/// She is on the player's side as far as anyone in this room can be. Her hostile lines are not
/// hostile, they are her going quiet and formal: she withdraws, she does not bite. Her open
/// lines are the tired coming through. Same shape as the stand-in script (one opening a round,
/// a hand node to close each one, a locked question to earn) so the wheel plays her the same way.
/// </remarks>
public static class SerenityDiscussion
{
    /// <summary>How long the box allows each discussion. The box sets the clock, not her, so it is the same as the stand-in's.</summary>
    public const float Seconds = 56f;

    /// <summary>A little warm before a word is said, and shy rather than shut. Guard has to reach 25 in one discussion to open HOW MANY?.</summary>
    public static readonly Disposition Opening = new(12, 36);

    /// <summary>Serenity's script.</summary>
    public static readonly DialogueScript Script = new()
    {
        OpponentId = "serenity",
        OpponentName = "SERENITY",
        Openings = new[] { "open", "second", "third", "fourth", "late" },
        Seconds = Seconds,
        Opens = Opening,
        Nodes = new DialogueNode[]
        {
            // ---- round one: sitting down ------------------------------------------------

            new()
            {
                Id = "open",
                Even = "Oh -- hello. Sorry, I did not hear you sit down. I am Serenity. You do not have to tell me yours if you would rather not.",
                Hostile = "Hello. Sorry. I will keep to my side of the table, if that is all right with you.",
                Open = "Hello. Sorry, I was somewhere else for a moment. I am Serenity. It is nice to have someone to talk to, even in here.",
                Options = new DialogueOption[]
                {
                    new("NICE TO MEET YOU", "It is nice to meet you, Serenity. I am {name}.",
                        Tone.Warm, Next: "name", Value: 2),

                    new("FIRST TIME", "This is my first time. I do not really know how this goes.",
                        Tone.Level, Next: "rules"),

                    // A kind question. The tone still costs guard, the line gives some of it back.
                    new("YOU LOOK TIRED", "Are you all right? You look tired.",
                        Tone.Probing, Next: "tired", Guard: -3, Flag: "serenity.asked.tired"),

                    new("SAVE IT", "You can save the manners. One of us is not getting up from this.",
                        Tone.Cutting, Next: "rules", Value: -2),
                },
            },

            new()
            {
                Id = "name",
                Even = "{name}. Thank you. I will try to say it right. I get names wrong when I am tired, and I am always tired now.",
                Hostile = "{name}. All right. I will remember it. I remember all of them.",
                Open = "{name}. That is a good name. I am going to hold on to that one.",
                Options = new DialogueOption[]
                {
                    // Carries most of the guard reduction on purpose. This is what opens HOW MANY? in the next beat.
                    new("YOU CAN REST", "You can rest a minute. I will not rush you.",
                        Tone.Warm, Next: "tired", Value: 3, Guard: -10),

                    new("IT'S ALL RIGHT", "It is all right if you get it wrong.",
                        Tone.Level, Next: "rules"),

                    new("ALL OF THEM?", "All of them? How long have you been here?",
                        Tone.Probing, Next: "tired", Guard: 4, Flag: "serenity.asked.howlong"),

                    new("DON'T BOTHER", "Do not bother remembering it.",
                        Tone.Cutting, Next: "rules", Value: -3),
                },
            },

            new()
            {
                Id = "rules",
                Even = "It lets us talk for a while, and then it wants our hands. Both of us. It gives something back, usually. I am sorry -- you probably wanted someone who could explain it better.",
                Hostile = "We talk. Then it takes our hands and gives something back. I am sorry. That is all I know how to say about it.",
                Open = "We talk until it is tired of listening, and then we both put a hand in. It gives you something. Sometimes it is nothing. I will tell you what I can, if you want.",
                Options = new DialogueOption[]
                {
                    new("THAT'S PLENTY", "That is plenty. Thank you, Serenity.",
                        Tone.Warm, Next: "close", Value: 4),

                    new("UNDERSTOOD", "Understood.",
                        Tone.Level, Next: "close"),

                    new("WHAT IS IT?", "What is it? The box. Do you know?",
                        Tone.Probing, Next: "close", Guard: 6, Flag: "serenity.asked.box"),

                    new("I DIDN'T ASK", "I did not ask for an apology.",
                        Tone.Cutting, Next: "close", Value: -3),
                },
            },

            new()
            {
                Id = "tired",
                Even = "I am. I have been sitting at this table a long time. It is polite of you to notice. Most people only look at the box.",
                Hostile = "I am fine. Thank you for asking. Please do not worry about me.",
                Open = "I am. I have not slept properly since I came here. I do not think anyone does. It is kind of you to ask, {name}.",
                Options = new DialogueOption[]
                {
                    new("REST A MOMENT", "Then rest a moment. I can wait.",
                        Tone.Warm, Next: "close", Value: 5, Guard: -4),

                    new("SO AM I", "So am I, and I only just sat down.",
                        Tone.Level, Next: "close"),

                    // Only for a player who has been gentle with her from the first line.
                    new("HOW MANY?", "How many people have sat where I am sitting?",
                        Tone.Probing, Next: "close", Guard: 8, Flag: "serenity.asked.count",
                        RequiresCandid: true),

                    new("THEN LOSE", "Then you will be slow. Good.",
                        Tone.Cutting, Next: "close", Value: -4),
                },
            },

            new()
            {
                Id = "close",
                Even = "It is done listening. I am sorry. Hands, I think. Yours and mine.",
                Hostile = "It wants our hands now. Please -- go ahead.",
                Open = "It is done listening. Hands, {name}. Whatever it gives you, do not look at me when you look at it. I will not look at you either.",
                Options = new DialogueOption[]
                {
                    new("GOOD LUCK", "Good luck, Serenity.", Tone.Warm, Value: 3),
                    new("READY", "Ready.", Tone.Level),
                    new("DOES IT HURT?", "Does it hurt?", Tone.Probing, Guard: 4),
                    new("JUST GO", "Just put it in.", Tone.Cutting),
                },
            },

            // ---- round two: after the first hand ----------------------------------------

            new()
            {
                Id = "second",
                Even = "You kept your hand in. That is good. I always pull mine back too soon, and it does not like that.",
                Hostile = "You got through it. I am glad. That is all I wanted to say.",
                Open = "You did not pull away. I still do, every time, and I have done this more times than I can count. Are you all right, {name}?",
                Options = new DialogueOption[]
                {
                    new("I'M ALL RIGHT", "I am all right. Are you?",
                        Tone.Warm, Next: "second.hand", Value: 3),

                    new("IT'S DONE", "It is done. That is the main thing.",
                        Tone.Level, Next: "second.hand"),

                    new("WHAT DID YOU GET?", "What did it give you?",
                        Tone.Probing, Next: "second.hand", Guard: 8, Flag: "serenity.asked.hand"),

                    new("DON'T PITY ME", "Do not talk to me like I am fragile.",
                        Tone.Cutting, Next: "second.hand", Value: -4),
                },
            },

            new()
            {
                Id = "second.hand",
                Even = "It wants them again. I am sorry. I keep saying that. I do not know what else to say.",
                Hostile = "Again. Please. Let us get it over with.",
                Open = "Again, {name}. Keep something back this time, if you can. I did not, my first nights, and I wish I had.",
                Options = new DialogueOption[]
                {
                    new("AFTER YOU", "After you, Serenity.", Tone.Warm, Value: 2),
                    new("READY", "Ready.", Tone.Level),
                    new("WHY KEEP IT?", "Why keep something back?", Tone.Probing, Guard: 5, Flag: "serenity.asked.pockets"),
                    new("STOP APOLOGISING", "Stop apologising. It is not helping.", Tone.Cutting, Value: -3),
                },
            },

            // ---- round three: counting ------------------------------------------------

            new()
            {
                Id = "third",
                Even = "Three. I stop being able to look at people around three. I am trying, with you. I would like you to know that.",
                Hostile = "Three hands. I am not going to count what you have against what I have. I do not want to know.",
                Open = "Three, {name}. This is usually where I go quiet. I do not want to, with you. I do not know if that is a good thing.",
                Options = new DialogueOption[]
                {
                    new("I'M HERE", "I am here. You can look.",
                        Tone.Warm, Next: "third.hand", Value: 4, Guard: -3),

                    new("IT'S ONLY THREE", "It is only three. We have both had worse nights.",
                        Tone.Level, Next: "third.hand"),

                    new("YOUR POCKETS?", "What are you carrying?",
                        Tone.Probing, Next: "third.hand", Guard: 9, Flag: "serenity.asked.carrying"),

                    new("YOU'RE SOFT", "You are soft. That is why you are still here and they are not.",
                        Tone.Cutting, Next: "third.hand", Value: -5),
                },
            },

            new()
            {
                Id = "third.hand",
                Even = "Hands, then. I hope it is kind to you. It has never been to me.",
                Hostile = "Hands. I will not make it worse than it has to be.",
                Open = "Hands, {name}. If it gives me something bad, I will try to keep it in my pocket. I cannot promise.",
                Options = new DialogueOption[]
                {
                    new("SAME FOR YOU", "I hope it is kind to you too.", Tone.Warm, Value: 2),
                    new("HANDS", "Hands.", Tone.Level),
                    new("YOU WOULDN'T?", "You would not use it on me?", Tone.Probing, Guard: 4),
                    new("DON'T PROMISE", "Do not promise me anything.", Tone.Cutting, Value: -2),
                },
            },

            // ---- round four: the quiet before something lands ---------------------------

            new()
            {
                Id = "fourth",
                Even = "It has been quiet. I do not trust the quiet. Sorry. I know that is not a comfort.",
                Hostile = "Something is going to happen this hand. I would rather it was me, if that helps.",
                Open = "Look at me for a second, {name}. Please. Before it does whatever it is going to do. I would like to have been looked at.",
                Options = new DialogueOption[]
                {
                    new("I'M LOOKING", "I am looking. I am right here.",
                        Tone.Warm, Next: "fourth.hand", Value: 4),

                    new("IT WON'T LAST", "The quiet never lasts. We both know that.",
                        Tone.Level, Next: "fourth.hand"),

                    // Only for a player she has been talked open by across three rounds.
                    new("HOW DOES IT END?", "How does this end? For the one who gets up.",
                        Tone.Probing, Next: "fourth.hand", Guard: 8, Flag: "serenity.asked.end",
                        RequiresCandid: true),

                    new("DON'T", "Do not make this into something.",
                        Tone.Cutting, Next: "fourth.hand", Value: -3),
                },
            },

            new()
            {
                Id = "fourth.hand",
                Even = "Hands.",
                Hostile = "Hands. Please.",
                Open = "Hands, {name}. If there is an after, I would like to talk in it. Properly. Not like this.",
                Options = new DialogueOption[]
                {
                    new("AFTER, THEN", "After, then. Properly.", Tone.Warm, Value: 2),
                    new("HANDS", "Hands.", Tone.Level),
                    new("AN AFTER?", "Is there an after? Have you seen one?", Tone.Probing, Guard: 4),
                    new("JUST DO IT", "Just put it in.", Tone.Cutting),
                },
            },

            // ---- every round after: running out of things to say -------------------------

            new()
            {
                Id = "late",
                Even = "I have run out of things to say. I am sorry. I am so tired.",
                Hostile = "I do not have anything left to say tonight. I am sorry.",
                Open = "We are still here, {name}. Both of us. I did not think we would be, and I am glad. That is a strange thing to be glad about.",
                Options = new DialogueOption[]
                {
                    new("STILL HERE", "Still here, Serenity.", Tone.Warm, Value: 2),
                    new("HANDS", "Hands.", Tone.Level),
                    new("WHY SO TIRED?", "Why are you so tired? What does it take out of you?", Tone.Probing, Guard: 4, Flag: "serenity.asked.why"),
                    new("FINISH IT", "Then let us finish it.", Tone.Cutting, Value: -2),
                },
            },
        },
    };
}
