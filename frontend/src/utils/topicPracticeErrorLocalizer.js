import { TEST_GENERATION_ERROR_MAP, getTestGenErrorMessage } from "./testGenerationErrorLocalizer";

export const TOPIC_PRACTICE_ERROR_MAP = TEST_GENERATION_ERROR_MAP;

export function getTopicPracticeErrorMessage(err, defaultMessage = "Thao tác thất bại. Vui lòng thử lại sau.") {
  return getTestGenErrorMessage(err, defaultMessage);
}
