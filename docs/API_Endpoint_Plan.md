# RaceDay API Endpoint Plan

Every endpoint the RaceDay API exposes. **Roles:** `Organiser`, `Participant`. `Any` means any logged-in user; `None` means public.

**Authentication is session based.** Logging in stores the user id and role in a server-side session; the client holds only an opaque session cookie. Protected endpoints return **401** when there is no session and **403** when the session's role is not permitted.

---

## Authentication

| HTTP method | Route | Description | Role required | Request body | Expected response |
|---|---|---|---|---|---|
| POST | /api/auth/register | Creates a new account as either an Organiser or a Participant. Password is hashed before storage. | None | { fullName, email, password, phone, role } | 201 Created - user object (no password)<br>400 Bad Request - validation error<br>409 Conflict - email already registered |
| POST | /api/auth/login | Authenticates the user and starts a session holding their id and role. | None | { email, password } | 200 OK - user object + session cookie<br>401 Unauthorized - invalid credentials |
| POST | /api/auth/logout | Clears the session. | Any | None | 204 No Content<br>401 Unauthorized - no active session |
| GET | /api/auth/me | Returns the current session user. Confirms session auth is working. | Any | None | 200 OK - user object<br>401 Unauthorized |

## User Profile

| HTTP method | Route | Description | Role required | Request body | Expected response |
|---|---|---|---|---|---|
| GET | /api/users/me | Returns the logged-in user's own profile. | Any | None | 200 OK - profile<br>401 Unauthorized |
| PUT | /api/users/me | Updates the logged-in user's own name and phone. Email and role are not editable. | Any | { fullName, phone } | 200 OK - updated profile<br>400 Bad Request<br>401 Unauthorized |
| PUT | /api/users/me/password | Changes own password after verifying the current one. | Any | { currentPassword, newPassword } | 200 OK - success message<br>400 Bad Request - current password wrong<br>401 Unauthorized |

## Events

| HTTP method | Route | Description | Role required | Request body | Expected response |
|---|---|---|---|---|---|
| GET | /api/events | Lists published events. Optional `location` and `eventType` query filters. | None | None | 200 OK - array of events |
| GET | /api/events/{id} | Returns one event with its categories. | None | None | 200 OK - event with categories<br>404 Not Found |
| GET | /api/events/mine | Lists events created by the logged-in Organiser, including drafts. | Organiser | None | 200 OK - array of events<br>401 Unauthorized<br>403 Forbidden |
| POST | /api/events | Creates an event owned by the logged-in Organiser. Captures name, description, date, location, distance and type. | Organiser | { name, description, eventDate, location, distanceKm, eventType, status } | 201 Created - event<br>400 Bad Request<br>401 Unauthorized<br>403 Forbidden - not an Organiser |
| PUT | /api/events/{id} | Updates an event. Only the owning Organiser may edit. | Organiser | { name, description, eventDate, location, distanceKm, eventType, status } | 200 OK - updated event<br>403 Forbidden - not the owner<br>404 Not Found |
| DELETE | /api/events/{id} | Deletes an event that has no enrolments. | Organiser | None | 204 No Content<br>403 Forbidden - not the owner<br>404 Not Found<br>409 Conflict - has enrolments |

## Categories

| HTTP method | Route | Description | Role required | Request body | Expected response |
|---|---|---|---|---|---|
| GET | /api/events/{eventId}/categories | Lists the age and distance categories for an event. | None | None | 200 OK - array of categories<br>404 Not Found - event |
| GET | /api/categories/{id} | Returns one category with its current enrolment count. | None | None | 200 OK - category<br>404 Not Found |
| POST | /api/events/{eventId}/categories | Adds a category to the Organiser's own event. Type is "Age" (with minAge/maxAge) or "Distance" (with distanceKm). | Organiser | { name, categoryType, distanceKm, minAge, maxAge, maxParticipants, entryFee } | 201 Created - category<br>400 Bad Request - missing fields for the type<br>403 Forbidden - not the owner<br>404 Not Found - event |
| PUT | /api/categories/{id} | Updates a category on the Organiser's own event. | Organiser | { name, categoryType, distanceKm, minAge, maxAge, maxParticipants, entryFee } | 200 OK - updated category<br>403 Forbidden<br>404 Not Found |
| DELETE | /api/categories/{id} | Deletes a category that has no enrolments. | Organiser | None | 204 No Content<br>403 Forbidden<br>404 Not Found<br>409 Conflict - has enrolments |

## Event Enrolments

| HTTP method | Route | Description | Role required | Request body | Expected response |
|---|---|---|---|---|---|
| POST | /api/categories/{categoryId}/enrolments | Participant enters an event by selecting a category. Records the participant-event-category link, assigns a bib number and creates a pending payment. | Participant | None | 201 Created - enrolment with bib number<br>401 Unauthorized<br>403 Forbidden - not a Participant<br>404 Not Found - category<br>409 Conflict - already enrolled or category full |
| GET | /api/enrolments/mine | Lists the logged-in Participant's own enrolments. | Participant | None | 200 OK - array of enrolments<br>401 Unauthorized<br>403 Forbidden |
| GET | /api/enrolments/{id} | Returns one enrolment. Visible to its Participant or the event's Organiser. | Any | None | 200 OK - enrolment<br>403 Forbidden<br>404 Not Found |
| GET | /api/categories/{categoryId}/enrolments | Lists all enrolments in a category, for the owning Organiser. | Organiser | None | 200 OK - array of enrolments<br>403 Forbidden - not the owner<br>404 Not Found |
| GET | /api/events/{eventId}/enrolments | Lists all enrolments across every category in one of the Organiser's events. | Organiser | None | 200 OK - array of enrolments<br>403 Forbidden - not the owner<br>404 Not Found |
| DELETE | /api/enrolments/{id} | Cancels an enrolment (marks it Cancelled). Participant may cancel their own; Organiser may cancel any in their event. | Any | None | 204 No Content<br>403 Forbidden<br>404 Not Found |

## Results

| HTTP method | Route | Description | Role required | Request body | Expected response |
|---|---|---|---|---|---|
| POST | /api/enrolments/{enrolmentId}/result | Captures finish time and finishing position for a participant's entry. | Organiser | { finishTime, position, status } | 201 Created - result<br>400 Bad Request<br>403 Forbidden - not the owner<br>404 Not Found - enrolment<br>409 Conflict - result already recorded |
| GET | /api/results/{id} | Returns a single result. | None | None | 200 OK - result<br>404 Not Found |
| PUT | /api/results/{id} | Corrects an existing result, e.g. a timing fix or a change to DNF/DSQ. | Organiser | { finishTime, position, status } | 200 OK - updated result<br>403 Forbidden<br>404 Not Found |
| GET | /api/categories/{categoryId}/results | Leaderboard: results for a category ranked by finish time, with DNF/DSQ last. | None | None | 200 OK - ranked array<br>404 Not Found - category |
| GET | /api/results/mine | Returns the logged-in Participant's own results. | Participant | None | 200 OK - array of results<br>401 Unauthorized<br>403 Forbidden |

---

## Changes from the original Part 1 plan

| Change | Reason |
|---|---|
| Removed the `RefreshTokens` entity and `POST /api/auth/refresh` | Part 2 requires **server-side session** authentication rather than JWT. Sessions are held server-side, so refresh tokens are not needed. Replaced with `GET /api/auth/me`. |
| Added `DistanceKm` and `EventType` to `Events` | Part 2 requires each event to capture a distance and an event type (run, walk or cycle). |
| Added `CategoryType`, `MinAge`, `MaxAge` to `Categories` | Part 2 requires categories to support **both** age categories (Under 20, Senior) and distance categories (10km, 21km). |
| Added `GET /api/events/{eventId}/enrolments` and `GET /api/categories/{id}` | Organisers need a single view of all entries across an event; the category detail route supports `CreatedAtAction` responses. |
