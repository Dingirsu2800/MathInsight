using MathInsight.Shared.Results;

namespace MathInsight.Modules.Learning_Lecture.Errors;

public static class LearningErrors
{
    public static readonly Error LectureRequestInvalid = new("LECTURE_REQUEST_INVALID", "Lecture request is required.");
    public static readonly Error LectureNotFound = new("LECTURE_NOT_FOUND", "Lecture was not found.");
    public static readonly Error LectureForbidden = new("LECTURE_FORBIDDEN", "You are not allowed to modify this lecture.");
    public static readonly Error LectureCannotUpdateDeactivated = new("LECTURE_DEACTIVATED", "A deactivated lecture cannot be updated.");
    public static readonly Error LecturePublishStateInvalid = new("LECTURE_PUBLISH_STATE_INVALID", "Only draft lectures can be published.");
    public static readonly Error LectureContentRequired = new("LECTURE_CONTENT_REQUIRED", "Lecture must have either video or content before publication.");
    public static readonly Error LectureTopicNotFound = new("LECTURE_TOPIC_NOT_FOUND", "Lecture topic was not found.");
    public static readonly Error LectureTopicInactive = new("LECTURE_TOPIC_INACTIVE", "Lecture topic is inactive.");
    public static readonly Error LectureDifficultyRequired = new("LECTURE_DIFFICULTY_REQUIRED", "Lecture difficulty is required.");
    public static readonly Error LectureDifficultyNotFound = new("LECTURE_DIFFICULTY_NOT_FOUND", "Lecture difficulty was not found.");
    public static readonly Error LectureDifficultyInactive = new("LECTURE_DIFFICULTY_INACTIVE", "Lecture difficulty is inactive.");
}
