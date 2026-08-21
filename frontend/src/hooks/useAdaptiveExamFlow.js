import { useState, useRef, useCallback } from "react";
import { useNavigate } from "react-router-dom";
import { testGeneratorApi } from "../services/testGeneratorApi";
import { startSession } from "../services/testingApi";
import { getTestGenErrorMessage } from "../utils/testGenerationErrorLocalizer";

export function useAdaptiveExamFlow() {
  const navigate = useNavigate();

  const [generating, setGenerating] = useState(false);
  const [starting, setStarting] = useState(false);
  const [generatedTestId, setGeneratedTestId] = useState(null);
  const [actionError, setActionError] = useState("");
  const [resumeSessionId, setResumeSessionId] = useState(null);

  const submittingRef = useRef(false);
  const generatedTestIdRef = useRef(null);

  const isBusy = generating || starting;

  const handleCreateAndStart = useCallback(async (blueprintId, onSuccess) => {
    if (submittingRef.current) return;

    if (resumeSessionId) {
      if (onSuccess) onSuccess();
      navigate(`/student/test/${resumeSessionId}`);
      return;
    }

    if (!blueprintId && !generatedTestIdRef.current) return;

    submittingRef.current = true;
    setActionError("");

    let testIdToStart = generatedTestIdRef.current;

    // Step 1: Generate Test if not already generated
    if (!testIdToStart) {
      setGenerating(true);
      try {
        const res = await testGeneratorApi.generateBlueprintExam(blueprintId);
        testIdToStart = res.data?.testId;
        if (!testIdToStart) {
          throw new Error("Không nhận được mã đề thi từ máy chủ.");
        }
        generatedTestIdRef.current = testIdToStart;
        setGeneratedTestId(testIdToStart);
      } catch (err) {
        submittingRef.current = false;
        setGenerating(false);
        setActionError(getTestGenErrorMessage(err, "Không thể tạo bài thi theo năng lực. Vui lòng thử lại sau."));
        return;
      } finally {
        setGenerating(false);
      }
    }

    // Step 2: Start session with the retained testId
    setStarting(true);
    try {
      const sessionData = await startSession(testIdToStart);
      const sessionId = sessionData?.sessionId || sessionData?.id;

      if (sessionId) {
        generatedTestIdRef.current = null;
        setGeneratedTestId(null);
        submittingRef.current = false;
        setStarting(false);
        if (onSuccess) onSuccess();
        navigate(`/student/test/${sessionId}`);
      } else {
        throw new Error("Không nhận được mã phiên làm bài từ máy chủ.");
      }
    } catch (err) {
      submittingRef.current = false;
      setStarting(false);

      const errCode = err.response?.data?.code;
      if (errCode === "TESTING_SESSION_ALREADY_IN_PROGRESS") {
        const existingSessionId = err.response?.data?.existingSessionId;
        if (existingSessionId && typeof existingSessionId === "string") {
          setResumeSessionId(existingSessionId);
        }
        setActionError("Bạn đang có một phiên làm bài chưa hoàn thành cho đề thi này.");
        return;
      }

      setActionError(getTestGenErrorMessage(err, "Không thể bắt đầu phiên làm bài. Vui lòng thử bắt đầu lại."));
    }
  }, [navigate, resumeSessionId]);

  const resetActionError = useCallback(() => {
    setActionError("");
  }, []);

  const resetFlow = useCallback(() => {
    if (submittingRef.current) return;
    setGenerating(false);
    setStarting(false);
    setActionError("");
    setResumeSessionId(null);
  }, []);

  return {
    generating,
    starting,
    isBusy,
    generatedTestId,
    actionError,
    resumeSessionId,
    handleCreateAndStart,
    resetActionError,
    resetFlow,
  };
}
