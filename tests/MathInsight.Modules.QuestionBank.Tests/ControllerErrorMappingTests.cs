using System.Reflection;
using System.Security.Claims;
using MathInsight.Modules.QuestionBank.Commands.DeleteQuestion;
using MathInsight.Modules.QuestionBank.Commands.UpdateTagTopic;
using MathInsight.Modules.QuestionBank.Contracts.Questions;
using MathInsight.Modules.QuestionBank.Contracts.Tags;
using MathInsight.Modules.QuestionBank.Controllers;
using MathInsight.Modules.QuestionBank.Errors;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MathInsight.Modules.QuestionBank.Tests;

public sealed class ControllerErrorMappingTests
{
    [Fact]
    public async Task DeleteQuestion_WhenHandlerReturnsQuestionInUse_Returns409WithStableCode()
    {
        var controller = new QuestionsController(CreateMediator(request =>
        {
            Assert.IsType<DeleteQuestionCommand>(request);
            return Result<DeleteQuestionResponse>.Failure(QuestionBankErrors.QuestionInUse);
        }))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = CreateAuthenticatedHttpContext()
            }
        };

        var result = await controller.DeleteQuestion("question-1", CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(conflict.Value);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
        Assert.Equal(QuestionBankErrors.QuestionInUse.Code, error.Code);
    }

    [Fact]
    public async Task UpdateTopic_WhenHandlerReturnsActiveDescendantsError_Returns409WithStableCode()
    {
        var controller = new TagsController(CreateMediator(request =>
        {
            Assert.IsType<UpdateTagTopicCommand>(request);
            return Result<TagTopicTreeResponse>.Failure(QuestionBankErrors.TagTopicHasActiveDescendants);
        }));

        var result = await controller.UpdateTopic(
            "topic-1",
            new UpdateTagTopicRequest
            {
                TagName = "Topic",
                Grade = 10,
                DisplayOrder = 1,
                IsActive = false
            },
            CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(conflict.Value);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
        Assert.Equal(QuestionBankErrors.TagTopicHasActiveDescendants.Code, error.Code);
    }

    private static IMediator CreateMediator(Func<object, object> responseFactory)
    {
        var mediator = DispatchProxy.Create<IMediator, MediatorStub>();
        ((MediatorStub)(object)mediator).ResponseFactory = responseFactory;
        return mediator;
    }

    private static DefaultHttpContext CreateAuthenticatedHttpContext()
    {
        var identity = new ClaimsIdentity(
            new[] { new Claim("account_id", "expert-1") },
            authenticationType: "Test");

        return new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity)
        };
    }

    public class MediatorStub : DispatchProxy
    {
        public Func<object, object> ResponseFactory { get; set; } = default!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "Send" && args?[0] is not null)
            {
                var response = ResponseFactory(args[0]!);
                var fromResult = typeof(Task).GetMethods()
                    .Single(method => method.Name == nameof(Task.FromResult) && method.IsGenericMethodDefinition);

                return fromResult.MakeGenericMethod(response.GetType()).Invoke(null, new[] { response });
            }

            throw new NotSupportedException($"Unsupported mediator method: {targetMethod?.Name}");
        }
    }
}
