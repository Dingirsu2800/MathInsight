using System.Collections.Generic;
using MediatR;
using MathInsight.Modules.Learning_Lecture.Contracts;

namespace MathInsight.Modules.Learning_Lecture.Queries.Materials;

public record GetMaterialListQuery(string TeacherId, int Page, int PageSize) : IRequest<List<MaterialDto>>;
