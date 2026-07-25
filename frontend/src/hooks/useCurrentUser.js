import * as React from "react";
import client from "../services/questionBankApiClient";
import { getAccessToken, getRoleName } from "../services/authStorage";

// UC-04. The authenticated user's own profile, shared by every surface that needs to show
// who is signed in (layout headers, dashboard greetings).
//
// Replaces authApi.getStoredUser(), which read localStorage keys ("token"/"UserName") that
// the login flow never writes — so it always returned null and callers silently fell back to
// the role label. authStorage.js is the single source of auth truth; the display name comes
// from the profile endpoint because the name is not stored client-side at all.
//
// The in-flight promise is cached at module scope so N components mounting together share
// ONE request instead of firing N. Cleared on sign-out so the next user never sees the
// previous user's name.

const PROFILE_URL = "/api/v1/accounts/profile";

let profilePromise = null;

function fetchProfileOnce() {
  if (!profilePromise) {
    profilePromise = client
      .get(PROFILE_URL)
      .then((response) => response.data || null)
      .catch((error) => {
        // Let the next mount retry rather than caching a failure forever. A 401 is already
        // handled by the client's refresh/redirect interceptor.
        profilePromise = null;
        throw error;
      });
  }
  return profilePromise;
}

/** Drop the cached profile — call on logout so the next session re-fetches. */
export function clearCachedProfile() {
  profilePromise = null;
}

/**
 * Builds the display name with the agreed fallback chain:
 * "firstName lastName" → username → roleLabel → "Người dùng".
 */
export function resolveDisplayName(profile, roleLabel) {
  const fullName = [profile?.firstName, profile?.lastName].filter(Boolean).join(" ").trim();
  return fullName || profile?.username || roleLabel || "Người dùng";
}

/** Initials for the avatar chip: first letter of the first and last word, else two chars. */
export function resolveInitials(displayName, fallback = "MI") {
  const parts = String(displayName || "").trim().split(/\s+/).filter(Boolean);
  if (parts.length >= 2) {
    return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
  }
  if (parts.length === 1) {
    return parts[0].substring(0, 2).toUpperCase();
  }
  return fallback;
}

/**
 * @param {string} [roleLabel] Shown only when the profile has neither a name nor a username.
 * @returns {{ profile: object|null, displayName: string, initials: string, loading: boolean }}
 */
export default function useCurrentUser(roleLabel) {
  const [profile, setProfile] = React.useState(null);
  // Unauthenticated surfaces must not fire the request at all.
  const [loading, setLoading] = React.useState(() => Boolean(getAccessToken()));

  React.useEffect(() => {
    let cancelled = false;

    if (!getAccessToken()) {
      setProfile(null);
      setLoading(false);
      return undefined;
    }

    setLoading(true);
    fetchProfileOnce()
      .then((data) => {
        if (!cancelled) setProfile(data);
      })
      .catch(() => {
        if (!cancelled) setProfile(null);
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, []);

  // While loading, fall back to the stored role name so the header never flashes "Người dùng".
  const displayName = resolveDisplayName(profile, roleLabel || getRoleName());

  return {
    profile,
    displayName,
    initials: resolveInitials(displayName),
    loading,
  };
}
