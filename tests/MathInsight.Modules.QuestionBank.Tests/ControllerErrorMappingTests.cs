using System.Reflection;
using System.Security.Claims;
using MathInsight.Modules.QuestionBank.Commands.AdminApproveQuestionReport;
using MathInsight.Modules.QuestionBank.Commands.AdminRejectQuestionReport;
using MathInsight.Modules.QuestionBank.Commands.DeleteQuestion;
using MathInsight.Modules.QuestionBank.Commands.ReportQuestion;
using MathInsight.Modules.QuestionBank.Commands.SubmitQuestionReportReview;
using MathInsight.Modules.QuestionBank.Commands.ToggleQuestionActive;
using MathInsight.Modules.QuestionBank.Commands.UploadQuestionImage;
using MathInsight.Modules.QuestionBank.Commands.ExtractQuestionOcrDraft;
using MathInsight.Modules.QuestionBank.Commands.UpdateTagTopic;
using MathInsight.Modules.QuestionBank.Contracts.Questions;
using MathInsight.Modules.QuestionBank.Contracts.Reports;
using MathInsight.Modules.QuestionBank.Contracts.Tags;
using MathInsight.Modules.QuestionBank.Controllers;
using MathInsight.Modules.QuestionBank.Errors;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MathInsight.Modules.QuestionBank.Tests;

public sealed class ControllerErrorMappingTests
{
    [Fact]
    public void QuestionImageEndpoint_RequiresExpertRole()
    {
        var authorizeAttribute = typeof(QuestionsController)
            .GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorizeAttribute);
        Assert.Equal("Expert", authorizeAttribute.Roles);
    }

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
    public async Task DeleteQuestion_WhenHandlerReturnsPendingReports_Returns409WithStableCode()
    {
        var controller = new QuestionsController(CreateMediator(request =>
        {
            Assert.IsType<DeleteQuestionCommand>(request);
            return Result<DeleteQuestionResponse>.Failure(QuestionBankErrors.QuestionHasPendingReports);
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
        Assert.Equal(QuestionBankErrors.QuestionHasPendingReports.Code, error.Code);
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

    [Fact]
    public async Task ReportQuestion_WhenHandlerReturnsDuplicatePending_Returns409WithStableCode()
    {
        var controller = new ReportsController(CreateMediator(request =>
        {
            Assert.IsType<ReportQuestionCommand>(request);
            return Result<ReportQuestionResponse>.Failure(QuestionBankErrors.ReportAlreadyPending);
        }))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = CreateAuthenticatedHttpContext("expert-1", "Expert")
            }
        };

        var result = await controller.ReportQuestion(
            "question-1",
            new ReportQuestionRequest { ReportReason = "Needs review." },
            CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(conflict.Value);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
        Assert.Equal(QuestionBankErrors.ReportAlreadyPending.Code, error.Code);
    }

    [Fact]
    public async Task ReportQuestion_WhenAdminWorkflowExists_Returns409WithStableCode()
    {
        var controller = new ReportsController(CreateMediator(request =>
        {
            Assert.IsType<ReportQuestionCommand>(request);
            return Result<ReportQuestionResponse>.Failure(QuestionBankErrors.AdminReportWorkflowAlreadyExists);
        }))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = CreateAuthenticatedHttpContext("admin-1", "Admin")
            }
        };

        var result = await controller.ReportQuestion(
            "question-1",
            new ReportQuestionRequest { ReportReason = "Needs review." },
            CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(conflict.Value);
        Assert.Equal(QuestionBankErrors.AdminReportWorkflowAlreadyExists.Code, error.Code);
    }

    [Fact]
    public async Task SubmitReview_WhenHandlerReturnsForbidden_Returns403WithStableCode()
    {
        var controller = new ReportsController(CreateMediator(request =>
        {
            Assert.IsType<SubmitQuestionReportReviewCommand>(request);
            return Result<QuestionReportResponse>.Failure(QuestionBankErrors.ReportAccessForbidden);
        }))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = CreateAuthenticatedHttpContext("expert-2", "Expert")
            }
        };

        var result = await controller.SubmitQuestionReportReview("report-1", CancellationToken.None);

        var forbidden = Assert.IsType<ObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(forbidden.Value);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
        Assert.Equal(QuestionBankErrors.ReportAccessForbidden.Code, error.Code);
    }

    [Fact]
    public async Task AdminApprove_WhenHandlerReturnsInvalidState_Returns409WithStableCode()
    {
        var controller = new ReportsController(CreateMediator(request =>
        {
            Assert.IsType<AdminApproveQuestionReportCommand>(request);
            return Result<QuestionReportResponse>.Failure(QuestionBankErrors.ReportAlreadyHandled);
        }))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = CreateAuthenticatedHttpContext("admin-1", "Admin")
            }
        };

        var result = await controller.ApproveQuestionReport("report-1", CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(conflict.Value);
        Assert.Equal(QuestionBankErrors.ReportAlreadyHandled.Code, error.Code);
    }

    [Fact]
    public async Task AdminReject_WhenHandlerReturnsInvalidReviewNote_Returns400WithStableCode()
    {
        var controller = new ReportsController(CreateMediator(request =>
        {
            Assert.IsType<AdminRejectQuestionReportCommand>(request);
            return Result<QuestionReportResponse>.Failure(QuestionBankErrors.ReviewNoteTooLong);
        }))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = CreateAuthenticatedHttpContext("admin-1", "Admin")
            }
        };

        var result = await controller.RejectQuestionReport(
            "report-1",
            new AdminRejectQuestionReportRequest { ReviewNote = new string('x', 2001) },
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(badRequest.Value);
        Assert.Equal(QuestionBankErrors.ReviewNoteTooLong.Code, error.Code);
    }

    [Fact]
    public async Task ToggleQuestion_WhenHandlerReturnsActiveReportError_Returns409WithStableCode()
    {
        var controller = new QuestionsController(CreateMediator(request =>
        {
            Assert.IsType<ToggleQuestionActiveCommand>(request);
            return Result<ToggleQuestionActiveResponse>.Failure(QuestionBankErrors.QuestionHasPendingReports);
        }))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = CreateAuthenticatedHttpContext()
            }
        };

        var result = await controller.ToggleQuestionActive(
            "question-1",
            new ToggleQuestionActiveRequest { IsActive = false },
            CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(conflict.Value);
        Assert.Equal(QuestionBankErrors.QuestionHasPendingReports.Code, error.Code);
    }

    [Theory]
    [InlineData("IMAGE_TOO_LARGE", StatusCodes.Status413PayloadTooLarge)]
    [InlineData("IMAGE_STORAGE_UNAVAILABLE", StatusCodes.Status503ServiceUnavailable)]
    [InlineData("IMAGE_UPLOAD_FAILED", StatusCodes.Status502BadGateway)]
    public async Task UploadQuestionImage_WhenHandlerReturnsMappedError_ReturnsStableHttpStatus(
        string errorCode,
        int expectedStatusCode)
    {
        var error = errorCode switch
        {
            "IMAGE_TOO_LARGE" => QuestionBankErrors.ImageTooLarge,
            "IMAGE_STORAGE_UNAVAILABLE" => QuestionBankErrors.ImageStorageUnavailable,
            "IMAGE_UPLOAD_FAILED" => QuestionBankErrors.ImageUploadFailed,
            _ => throw new InvalidOperationException("Unexpected error code.")
        };
        var controller = new QuestionsController(CreateMediator(request =>
        {
            Assert.IsType<UploadQuestionImageCommand>(request);
            return Result<QuestionImageUploadResponse>.Failure(error);
        }));

        var result = await controller.UploadQuestionImage(null, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        var response = Assert.IsType<ApiErrorResponse>(objectResult.Value);
        Assert.Equal(expectedStatusCode, objectResult.StatusCode);
        Assert.Equal(error.Code, response.Code);
    }

    [Theory]
    [InlineData("OCR_NOT_CONFIGURED", StatusCodes.Status503ServiceUnavailable)]
    [InlineData("OCR_PROVIDER_UNAVAILABLE", StatusCodes.Status502BadGateway)]
    [InlineData("OCR_PROVIDER_RATE_LIMITED", StatusCodes.Status429TooManyRequests)]
    [InlineData("OCR_TIMEOUT", StatusCodes.Status504GatewayTimeout)]
    [InlineData("OCR_INVALID_RESPONSE", StatusCodes.Status502BadGateway)]
    [InlineData("OCR_DRAFT_UNAVAILABLE", StatusCodes.Status422UnprocessableEntity)]
    public async Task ExtractQuestionOcrDraft_WhenHandlerReturnsMappedError_ReturnsStableHttpStatus(
        string errorCode,
        int expectedStatusCode)
    {
        var error = errorCode switch
        {
            "OCR_NOT_CONFIGURED" => QuestionBankErrors.OcrNotConfigured,
            "OCR_PROVIDER_UNAVAILABLE" => QuestionBankErrors.OcrProviderUnavailable,
            "OCR_PROVIDER_RATE_LIMITED" => QuestionBankErrors.OcrProviderRateLimited,
            "OCR_TIMEOUT" => QuestionBankErrors.OcrTimeout,
            "OCR_INVALID_RESPONSE" => QuestionBankErrors.OcrInvalidResponse,
            "OCR_DRAFT_UNAVAILABLE" => QuestionBankErrors.OcrDraftUnavailable,
            _ => throw new InvalidOperationException("Unexpected error code.")
        };
        var controller = new QuestionsController(CreateMediator(request =>
        {
            Assert.IsType<ExtractQuestionOcrDraftCommand>(request);
            return Result<QuestionOcrDraftResponse>.Failure(error);
        }));

        var result = await controller.ExtractQuestionOcrDraft(null, CancellationToken.None);

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        var response = Assert.IsType<ApiErrorResponse>(objectResult.Value);
        Assert.Equal(expectedStatusCode, objectResult.StatusCode);
        Assert.Equal(error.Code, response.Code);
    }

    private static IMediator CreateMediator(Func<object, object> responseFactory)
    {
        var mediator = DispatchProxy.Create<IMediator, MediatorStub>();
        ((MediatorStub)(object)mediator).ResponseFactory = responseFactory;
        return mediator;
    }

    private static DefaultHttpContext CreateAuthenticatedHttpContext(
        string accountId = "expert-1",
        string? role = null)
    {
        var claims = new List<Claim> { new("account_id", accountId) };
        if (!string.IsNullOrWhiteSpace(role))
            claims.Add(new Claim(ClaimTypes.Role, role));

        var identity = new ClaimsIdentity(claims, authenticationType: "Test");

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
