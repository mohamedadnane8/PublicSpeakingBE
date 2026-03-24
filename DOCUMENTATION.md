# Impromptu - API & Frontend Documentation

## 1. Project Overview

**Impromptu** is a public speaking practice app with real-time recording, AI-powered speech analysis, and interview prep.

| Layer | Stack |
|-------|-------|
| Frontend | React 19, Vite 7, TypeScript, Tailwind CSS, Radix UI |
| Backend | ASP.NET Core 10 (.NET), Clean Architecture |
| Database | PostgreSQL (EF Core) |
| Auth | Google OAuth 2.0 + JWT (HttpOnly cookies) |
| Storage | AWS S3 (audio files) |
| AI | DeepSeek (speech analysis, question generation) |
| Transcription | Deepgram (nova-3), AssemblyAI (alternative) |

**Auth model:** Backend sets HttpOnly `refresh_token` and `session_id` cookies. Frontend stores the access token in-memory only (not localStorage). The API client automatically refreshes on 401. All token refresh logic is centralized in `apiClient.ts` with a single `API_BASE_URL` export.

---

## 2. API Reference

Base URL: configured via `VITE_API_BASE_URL` (default: `https://api.publicspeaking.adnanelogs.com`)

### 2.1 Health

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/health` | None | Returns `{status, timestamp, version}` |
| GET | `/health/detailed` | None | Includes database connectivity check. Returns 503 if unhealthy |

### 2.2 Auth

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/api/auth/google/login?redirectUri=` | None | Redirects (302) to Google OAuth. Sets PKCE cookies (`oauth_state`, `oauth_code_verifier`, `oauth_redirect_uri`) |
| GET | `/api/auth/google/callback` | None | Google redirects here. Exchanges code for tokens, sets `access_token`, `refresh_token`, `session_id` cookies. Redirects to frontend |
| GET | `/api/auth/me` | Bearer | Returns current user info |
| POST | `/api/auth/refresh` | Cookie | Rotates refresh token, returns `{accessToken, success}`. Returns 409 if token already rotated (retry safe) |
| POST | `/api/auth/logout` | Cookie | Body: `{revokeAllSessions?: boolean}`. Clears all auth cookies |
| POST | `/api/auth/dev/login` | None | **Dev only.** Body: `{email?, firstName?, lastName?}`. Creates test user + sets cookies |

**`GET /api/auth/me` response:**
```json
{
  "id": "guid",
  "email": "string",
  "firstName": "string",
  "lastName": "string?",
  "displayName": "string",
  "profilePictureUrl": "string?",
  "createdAt": "datetime",
  "lastLoginAt": "datetime"
}
```

### 2.3 Sessions

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/api/sessions` | Bearer | Create session (JSON body). Returns 201 + `SessionDto` |
| POST | `/api/sessions/record` | Bearer | **Multipart:** `audio` (file, max 10MB) + `session` (JSON string). Creates session + starts transcription. Returns 201 |
| GET | `/api/sessions` | Bearer | Returns array of user's sessions |
| GET | `/api/sessions/{id}` | Bearer | Returns single session. 403 if not owner |
| PUT | `/api/sessions/{id}` | Bearer | Partial update (ratings, notes, status). Only non-null fields applied |
| DELETE | `/api/sessions/{id}` | Bearer | Returns 204. 403 if not owner |
| GET | `/api/sessions/{id}/audio-url` | Bearer | Returns `{url, expiresAt}` presigned S3 URL |
| POST | `/api/sessions/{id}/analyze?reanalyze=false` | Bearer | Triggers AI analysis. 429 on rate limit, 502 on AI failure |

**`CreateSessionRequest`:**
```json
{
  "id": "guid",
  "createdAt": "datetime",
  "completedAt": "datetime?",
  "mode": "Impromtu | Free",
  "type": "General | Behavioral",
  "language": "EN | ES | FR | ...",
  "difficulty": "EASY | MEDIUM | HARD",
  "word": "string",
  "thinkSeconds": 30,
  "speakSeconds": 120,
  "status": "Created | Completed | Cancelled",
  "cancelReason": "string?",
  "ratings": { "opening": 1-5, "structure": 1-5, "ending": 1-5, "confidence": 1-5, "clarity": 1-5, "authenticity": 1-5, "languageExpression": 1-5, "passion": 1-5 },
  "interviewRatings": { "relevance": 1-5, "situationStakes": 1-5, "action": 1-5, "resultImpact": 1-5, "deliveryComposure": 1-5, "conciseness": 1-5 },
  "notes": "string?",
  "audio": { "available": true, "durationMs": 60000, ... },
  "transcript": "string?"
}
```

**`SessionDto` response** includes all above fields plus: `userId`, `manualScore`, `aiScore`, `transcriptionStatus`, `transcriptionError`, `advice`, `speechAnalysis` (raw AI JSON), `analyzedAt`, `aiScored`.

**Score mapping note:** The backend returns `manualScore` and `aiScore` (computed server-side from ratings and AI analysis). The frontend maps these to its `overallScore` field when loading history sessions: `overallScore = manualScore ?? aiScore`.

**`UpdateSessionRequest`:** Partial - only `status`, `cancelReason`, `completedAt`, `ratings`, `interviewRatings`, `notes`.

**Audio formats:** mp3, wav, m4a, webm, ogg (max 10MB)

### 2.4 Audio

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/api/audio/upload` | Bearer | Multipart file upload. Max 10MB. Returns `{objectKey, bucketName, region, fileSize, contentType}` |

### 2.5 Resume

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/api/resume/parse` | Bearer | Multipart (PDF/DOCX, max 10MB). Parses resume + generates interview questions via DeepSeek. **Rate limit: 5 uploads/week** |
| GET | `/api/resume/questions/categories` | Bearer | Returns `string[]` of question categories from user's resume |
| GET | `/api/resume/questions/random?difficulty=&category=` | Bearer | Returns random interview question from parsed resume |
| GET | `/api/resume/questions/behavioral/random?language=&difficulty=` | Bearer | Returns random behavioral question from static pool |

**`POST /api/resume/parse` response:**
```json
{
  "fileName": "string",
  "contentType": "string",
  "pageCount": 3,
  "questionsGenerated": 15,
  "detectedLanguage": "English",
  "detectedField": "Software Engineering"
}
```

**`InterviewQuestionDto`:**
```json
{
  "id": "guid",
  "question": "string",
  "category": "string",
  "difficulty": "Easy | Medium | Hard",
  "thinkingSeconds": 30,
  "answeringSeconds": 120
}
```

**Rate limit error (429):**
```json
{
  "error": "weekly_limit_reached",
  "message": "...",
  "uploadsUsed": 5,
  "maxUploadsPerWeek": 5,
  "nextSlotAt": "2024-01-15T10:00:00Z"
}
```

### 2.6 Feature Requests

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/api/feature-requests` | Bearer | Body: `{message: string (3-2000 chars), pageUrl?: string (max 500)}`. Returns 201 |
| GET | `/api/feature-requests` | Bearer | Returns user's feature requests |

**`FeatureRequestDto`:**
```json
{
  "id": "guid",
  "userId": "guid",
  "message": "string",
  "pageUrl": "string?",
  "createdAt": "datetime"
}
```

### 2.7 Words

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/api/words/random?language=&difficulty=&exclude=` | **None** | Returns `{word, language, difficulty}`. `exclude` param repeatable to skip words |

---

## 3. Frontend Architecture

### 3.1 API Client Layer

**`src/lib/apiClient.ts`** - Centralized fetch wrapper:
- Exports `API_BASE_URL` (single source of truth, used by all modules)
- Stores access token in-memory (not localStorage)
- Auto-attaches `Authorization: Bearer <token>` to every request
- On 401: queues the request, performs token refresh, retries once
- Refresh queue deduplicates concurrent refresh calls
- Circuit breaker: stops after 3 consecutive refresh failures
- Handles 409 (token already rotated) with exponential backoff retry
- All requests use `credentials: "include"` for cross-site cookies

**`src/lib/interviewApi.ts`** - Typed API wrappers:
- `parseResume(file)` - resume upload with rate limit error handling
- `fetchRandomQuestion(difficulty?, category?)` - interview questions
- `fetchBehavioralQuestion(language?, difficulty?)` - behavioral questions
- `fetchSession(sessionId)` - single session fetch
- `analyzeSession(sessionId, reanalyze?)` - trigger AI analysis
- `recordSession(audioFile, sessionData)` - multipart session + audio creation
- `updateSession(sessionId, data)` - partial session update
- `fetchCategories()` - resume question categories

**`src/lib/words.ts`** - Standalone fetch (no auth required, imports `API_BASE_URL` from apiClient):
- `fetchRandomWordFromBackend({language, difficulty, excludedWords})`

### 3.2 Context Providers (State Management)

The app uses React Context for state management, split into four providers:

| Context | File | Responsibility |
|---------|------|---------------|
| `AppContext` | `contexts/AppContext.tsx` | Screen navigation, auth state (wraps `useAuth`), playback controls, account menu, navbar |
| `SessionContext` | `contexts/SessionContext.tsx` | Session save/sync, remote sessions, early save, transcription polling, history operations |
| `PracticeContext` | `contexts/PracticeContext.tsx` | General practice flow: mode config, word state, timers, ratings, sound system |
| `InterviewContext` | `contexts/InterviewContext.tsx` | Interview flow: question reveal, interview timers, STAR ratings, resume check |

**Provider tree** (in `App.tsx`):
```
AppProvider > SessionProvider > PracticeProvider > InterviewProvider > AppLayout
```

### 3.3 Custom Hooks

| Hook | File | Responsibility |
|------|------|---------------|
| `useAuth` | `hooks/useAuth.ts` | User state, login/logout, OAuth redirect, dev login. `refreshUser` delegates to `initializeAuth()` (centralized in apiClient) |
| `useSession` | `hooks/useSession.ts` | Local session CRUD with localStorage persistence |
| `useInterview` | `hooks/useInterview.ts` | Resume upload state, question fetching, category management |
| `useAudioRecorder` | `hooks/useAudioRecorder.ts` | MediaRecorder wrapper for mic recording. Properly cleans up previous recorder on new recording start |
| `useTranscription` | `hooks/useTranscription.ts` | Browser SpeechRecognition API (client-side, real-time) |
| `useTranscriptionPolling` | `hooks/useTranscriptionPolling.ts` | Polls `GET /api/sessions/{id}` every 3s for server-side transcription. Resets state on sessionId change. Max 2min timeout |
| `useTimer` | `hooks/useTimer.ts` | Timer with pause/resume |
| `useSoundSystem` | `hooks/useSoundSystem.ts` | Audio playback utilities |
| `useSessionStorage` | `hooks/useSessionStorage.ts` | localStorage persistence wrapper |

### 3.4 Shared Components

| Component | File | Purpose |
|-----------|------|---------|
| `ErrorBoundary` | `components/ErrorBoundary.tsx` | Root-level error boundary. Catches render crashes, shows fallback UI with retry |
| `AudioPlayer` | `components/AudioPlayer.tsx` | Reusable playback controls (seek bar, play/pause, skip +-5s). Supports `compact` mode. Used in PlaybackScreen, ScoreSummaryScreen, and HistoryScreen |
| `AiAnalysis` | `components/AiAnalysis.tsx` | AI analysis display with radar chart (Recharts), filler analysis, per-criterion breakdown, strength/improvement callouts. Rate-limited to 3/day |
| `TopNavbar` | `components/TopNavbar.tsx` | Navigation header with section tabs and account menu |
| `WordReveal` | `components/WordReveal.tsx` | Letter-by-letter word reveal animation |
| `QuestionReveal` | `components/QuestionReveal.tsx` | Word-by-word question reveal animation |
| `CircularProgress` | `components/CircularProgress.tsx` | SVG circular timer display |
| `RatingDots` | `components/RatingDots.tsx` | 5-dot rating selector |

### 3.5 Flow Components

| Component | File | Renders |
|-----------|------|---------|
| `GeneralFlow` | `components/GeneralFlow.tsx` | HOME, WORD_REVEAL, THINK, SPEAK, PLAYBACK, REFLECT, SCORE_SUMMARY |
| `InterviewFlow` | `components/InterviewFlow.tsx` | INTERVIEW_HOME, INTERVIEW_QUESTION, INTERVIEW_THINK, INTERVIEW_SPEAK, INTERVIEW_PLAYBACK, INTERVIEW_REFLECT, INTERVIEW_SCORE |
| `UtilityScreens` | `components/UtilityScreens.tsx` | HISTORY, FEATURE_REQUEST, auth callbacks |

### 3.6 Screen Flow

**General Mode:**
```
HOME -> WORD_REVEAL -> THINK -> SPEAK -> PLAYBACK -> REFLECT -> SCORE_SUMMARY
```

**Interview Mode:**
```
INTERVIEW_HOME -> INTERVIEW_QUESTION -> INTERVIEW_THINK -> INTERVIEW_SPEAK -> INTERVIEW_PLAYBACK -> INTERVIEW_REFLECT -> INTERVIEW_SCORE
```

Screen routing is managed via a `screen` state in `AppContext`, rendered by the flow components.

---

## 4. API-Frontend Alignment

| Backend Endpoint | Frontend Consumer | Notes |
|---|---|---|
| `GET /api/auth/google/login` | `useAuth.login()` via `window.location.href` | Full page redirect |
| `GET /api/auth/google/callback` | Handled by backend (redirects to frontend) | Sets cookies automatically |
| `GET /api/auth/me` | `apiClient("/api/auth/me")` in `initializeAuth()` | Called on app mount + after login |
| `POST /api/auth/refresh` | `performRefresh()` in `apiClient.ts` | Auto-triggered on 401 |
| `POST /api/auth/logout` | `logout()` in `apiClient.ts` via `useAuth.logout()` | |
| `POST /api/auth/dev/login` | `useAuth.devLogin()` via raw `fetch` | Dev mode only |
| `POST /api/sessions` | Used as fallback in `SessionContext` | Primary path is `/api/sessions/record` |
| `POST /api/sessions/record` | `recordSession()` in `interviewApi.ts` | Multipart: audio + session JSON |
| `GET /api/sessions` | `loadRemoteSessions()` in `SessionContext` | History screen. Maps `manualScore`/`aiScore` to `overallScore` |
| `GET /api/sessions/{id}` | `fetchSession()` in `interviewApi.ts` | Used by transcription polling |
| `PUT /api/sessions/{id}` | `updateSession()` in `interviewApi.ts` | After self-rating |
| `DELETE /api/sessions/{id}` | `handleDeleteHistorySession()` in `SessionContext` | History screen |
| `GET /api/sessions/{id}/audio-url` | `HistoryAudioPlayer` in `HistoryScreen.tsx` | Fetches presigned URL for history playback |
| `POST /api/sessions/{id}/analyze` | `analyzeSession()` in `interviewApi.ts` | AI analysis trigger |
| `POST /api/audio/upload` | Fallback in `saveSessionAndGetAdvice` | Used when early save fails |
| `POST /api/resume/parse` | `parseResume()` in `interviewApi.ts` | Via `useInterview.uploadResume()` |
| `GET /api/resume/questions/categories` | `fetchCategories()` in `interviewApi.ts` | |
| `GET /api/resume/questions/random` | `fetchRandomQuestion()` in `interviewApi.ts` | |
| `GET /api/resume/questions/behavioral/random` | `fetchBehavioralQuestion()` in `interviewApi.ts` | |
| `POST /api/feature-requests` | `apiClient` in `FeatureRequestScreen.tsx` | |
| `GET /api/feature-requests` | `apiClient` in `FeatureRequestScreen.tsx` | |
| `GET /api/words/random` | `fetchRandomWordFromBackend()` in `words.ts` | No auth, standalone fetch |
| `GET /health`, `GET /health/detailed` | Not called by frontend | Infrastructure endpoints |

---

## 5. Issues & Anti-Patterns

### Resolved

The following issues from the initial audit have been fixed:

- **Error Boundary** - Added `ErrorBoundary` component wrapping the app in `main.tsx`
- **Monolithic App.tsx** - Decomposed from 1799 lines to 151 lines via 4 context providers + 3 flow components
- **Token refresh race condition** - `useAuth.refreshUser()` now delegates to `initializeAuth()` (single path through `apiClient.ts`)
- **Duplicate API_BASE_URL** - Single export from `apiClient.ts`, imported by `useAuth.ts` and `words.ts`
- **Stale transcript bug** - `useTranscriptionPolling` now resets state when `sessionId` changes
- **History score not showing** - `loadRemoteSessions` now maps backend `manualScore`/`aiScore` to frontend `overallScore`
- **Simple replay buttons** - Replaced with reusable `AudioPlayer` component (seek bar, skip, play/pause) on ScoreSummaryScreen and HistoryScreen
- **AI Analysis visualization** - Added Recharts radar chart for criteria scores above the text breakdown
- **Audio recorder corruption** - `useAudioRecorder.startRecording()` now properly stops previous recorder and nulls its event handlers before resetting chunks

### Remaining

**High:**

1. **No API Response Validation**
   - **Where:** All API calls in `interviewApi.ts`, `useAuth.ts`
   - **Impact:** Responses cast with `as Type` without runtime validation
   - **Fix:** Add zod schemas (already in `package.json`)

2. **Unhandled Promise Rejections**
   - **Where:** `void saveSessionEarly(...)`, `void loadRemoteSessions()` patterns in contexts
   - **Impact:** Async errors silently swallowed
   - **Fix:** Add `.catch()` with user-facing error toasts

3. **Silent Failures in Transcription Polling**
   - **Where:** `useTranscriptionPolling.ts` - empty `catch {}` block during polling
   - **Impact:** Network errors swallowed, could poll indefinitely
   - **Fix:** Log errors, count consecutive failures, show retry indicator

**Medium:**

4. **No AbortController Usage**
   - **Where:** All API calls across the frontend
   - **Impact:** Long-running requests continue after navigation
   - **Fix:** Use AbortController in hooks, cancel on unmount

5. **No Offline Detection**
   - **Where:** Entire frontend
   - **Fix:** Add `navigator.onLine` listener with global banner

6. **`any` Types in useTranscription**
   - **Where:** `hooks/useTranscription.ts`
   - **Fix:** Add proper `SpeechRecognition` type declarations

**Low:**

7. **Unused Dependency** - `react-hook-form` in `package.json` but never imported
8. **Deprecated Export** - `fetchWithAuth` in `useAuth.ts` marked `@deprecated`
9. **No Frontend Tests** - Zero test files exist

---

## 6. Recommended Improvements (Prioritized)

1. **Add zod response validation** - Already in package.json, use it for all API responses
2. **Add AbortController** - To all API calls, cancel on component unmount
3. **Handle fire-and-forget promises** - Add `.catch()` with toast error feedback
4. **Fix transcription polling** - Add error counting and user feedback in catch block
5. **Add offline detection** - `navigator.onLine` listener with global UI indicator
6. **Add frontend tests** - vitest + RTL for scoring logic, hooks, and API layer
7. **Remove unused deps** - `react-hook-form` and deprecated `fetchWithAuth`
