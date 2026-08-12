namespace MathInsight.Modules.TestGen.Generation;

public static class GeneratedTestValues
{
    public const string ActiveStatus = "Active";
    public const string ArchivedStatus = "Archived";
    public const string BlueprintExamMode = "BlueprintExam";
    public const string SystemGenerator = "System";
    public const string ExpertGenerator = "Expert";
    public const string BlueprintNormalReason = "BlueprintNormal";
    public const string FixedExamReason = "FixedExam";
    public const string RandomGenerationType = "Random";
    public const string FixedGenerationType = "Fixed";

    public static string ToGenerationType(bool isFixed)
        => isFixed ? FixedGenerationType : RandomGenerationType;
}
