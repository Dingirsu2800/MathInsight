using System.IO;
using MediatR;

namespace MathInsight.Modules.Learning_Lecture.Commands.Lectures;

public record UploadLectureThumbnailCommand(Stream FileStream, string FileName) : IRequest<string>;
