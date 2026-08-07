import * as React from "react";
import { useLocation } from "react-router-dom";

const NavigationGuardContext = React.createContext(null);

export function NavigationGuardProvider({ children }) {
  const guardRef = React.useRef(null);
  const currentHistoryIndexRef = React.useRef(window.history.state?.idx ?? null);
  const isRestoringHistoryRef = React.useRef(false);
  const location = useLocation();

  const registerGuard = React.useCallback((guard) => {
    guardRef.current = guard;
    return () => {
      if (guardRef.current === guard) guardRef.current = null;
    };
  }, []);

  const confirmNavigation = React.useCallback(() => {
    return !guardRef.current || guardRef.current();
  }, []);

  React.useEffect(() => {
    currentHistoryIndexRef.current = window.history.state?.idx ?? null;
  }, [location.key, location.pathname, location.search]);

  React.useEffect(() => {
    const handlePopState = (event) => {
      if (isRestoringHistoryRef.current) {
        isRestoringHistoryRef.current = false;
        currentHistoryIndexRef.current = event.state?.idx ?? null;
        return;
      }

      const nextHistoryIndex = event.state?.idx;
      const currentHistoryIndex = currentHistoryIndexRef.current;
      const canRestoreHistory =
        typeof currentHistoryIndex === "number" &&
        typeof nextHistoryIndex === "number" &&
        currentHistoryIndex !== nextHistoryIndex;

      if (confirmNavigation()) {
        currentHistoryIndexRef.current = nextHistoryIndex ?? null;
        return;
      }

      if (!canRestoreHistory) {
        // BrowserRouter always provides an index. This fallback protects a Back action
        // from a legacy entry that does not carry React Router state.
        isRestoringHistoryRef.current = true;
        window.history.go(1);
        return;
      }

      isRestoringHistoryRef.current = true;
      window.history.go(nextHistoryIndex > currentHistoryIndex ? -1 : 1);
    };

    window.addEventListener("popstate", handlePopState);
    return () => window.removeEventListener("popstate", handlePopState);
  }, [confirmNavigation]);

  const value = React.useMemo(
    () => ({ registerGuard, confirmNavigation }),
    [registerGuard, confirmNavigation]
  );

  return (
    <NavigationGuardContext.Provider value={value}>
      {children}
    </NavigationGuardContext.Provider>
  );
}

export function useNavigationGuard() {
  const context = React.useContext(NavigationGuardContext);
  return context || {
    registerGuard: () => () => {},
    confirmNavigation: () => true
  };
}
