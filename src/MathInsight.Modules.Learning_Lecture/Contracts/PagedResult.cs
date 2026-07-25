using System.Collections.Generic;

namespace MathInsight.Modules.Learning_Lecture.Contracts;

public record PagedResult<T>(List<T> Items, int TotalCount);
