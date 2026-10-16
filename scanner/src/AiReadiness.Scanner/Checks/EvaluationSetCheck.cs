using System.Text.RegularExpressions;
using AiReadiness.Scanner.Domain;

namespace AiReadiness.Scanner.Checks;

public sealed partial class EvaluationSetCheck : IReadinessCheck
{
    public string Area => "Measured quality (evaluation set)";

    public IReadOnlyList<Criterion> Evaluate(IRepoFiles repo)
    {
        var text = repo.ReadText("evals/questions.yaml");
        var questions = text is null ? 0 : QuestionId().Matches(text).Count;

        return
        [
            new(Area, "evals/questions.yaml exists", 4, text is not null,
                "Create evals/questions.yaml: real questions your team asks, each with the source that answers it."),
            new(Area, "At least 10 questions", 3, questions >= 10,
                "Write 10 questions from real Slack threads and onboarding doubts."),
            new(Area, "At least 30 questions", 3, questions >= 30,
                "Grow the set to 30 so a score change means something."),
        ];
    }

    [GeneratedRegex(@"^\s*-\s+id:", RegexOptions.Multiline)]
    private static partial Regex QuestionId();
}
