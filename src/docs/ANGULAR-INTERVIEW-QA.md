# Angular Interview Q&A — CredVault

## Beginner

### 1. What is the difference between `@Component` and `@Directive`?

| Aspect | `@Component` | `@Directive` |
|--------|-------------|--------------|
| Purpose | Creates a UI view with template + styles | Adds behavior to existing elements |
| Template | Has its own template (`templateUrl` or `template`) | No template — modifies host element |
| Use case | Building UI pieces (navbar, card, form) | Adding logic to elements (highlight, validation, click handlers) |
| In your project | All feature components — `LoginComponent`, `DashboardComponent` | Not explicitly used, but `*ngIf`, `*ngFor` are structural directives |

Example:
```typescript
// Component — renders UI
@Component({ selector: 'app-card', template: '<div>Card content</div>' })
class CardComponent {}

// Directive — adds behavior
@Directive({ selector: '[appHighlight]' })
class HighlightDirective {
  constructor(el: ElementRef) {
    el.nativeElement.style.backgroundColor = 'yellow';
  }
}
```

---

### 2. How does data binding work in Angular?

Four types:

| Type | Syntax | Direction | Example |
|------|--------|-----------|---------|
| **Interpolation** | `{{ value }}` | Component → Template | `{{ user.fullName }}` |
| **Property Binding** | `[property]="value"` | Component → Template | `[disabled]="isLoading()"` |
| **Event Binding** | `(event)="handler()"` | Template → Component | `(click)="onSubmit()"` |
| **Two-Way Binding** | `[(ngModel)]="value"` | Both directions | `[(ngModel)]="email"` |

New Angular 17+ control flow replaces `*ngIf`/`*ngFor`:
```html
@if (isLoading()) { <p>Loading...</p> } @else { <p>Data loaded</p> }
@for (card of cards(); track card.id) { <app-card [card]="card" /> }
```

---

### 3. What are standalone components?

Standalone components don't require an `NgModule`. Every component/directive/pipe declares its own dependencies via the `imports` array.

```typescript
// YOUR PROJECT — all components are standalone
@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink, IstDatePipe],
  templateUrl: './dashboard.component.html'
})
export class DashboardComponent { ... }
```

**Why it matters:** Simpler architecture, no boilerplate NgModules, easier lazy loading, better tree-shaking.

---

### 4. Difference between `constructor` and `ngOnInit`?

| Aspect | `constructor` | `ngOnInit` |
|--------|--------------|------------|
| When it runs | When the class is instantiated by DI | After Angular initializes all inputs and bindings |
| Purpose | Initialize class fields, inject dependencies | Component setup that needs inputs, DOM access, or async calls |
| In your project | `FormBuilder` setup in `dashboard.component.ts` | `loadDashboardData()` and `loadIssuers()` in `ngOnInit()` |

```typescript
// Constructor — dependency injection only
constructor() {
  this.addCardForm = this.fb.group({ ... });
}

// ngOnInit — component initialization
ngOnInit(): void {
  this.loadDashboardData();  // API call needs component to be fully initialized
  this.loadIssuers();
}
```

---

### 5. How do you share data between components?

| Method | Use Case | Example |
|--------|----------|---------|
| **Service with signals** | Cross-component state (auth, theme) | `AuthService.currentUser` signal |
| **`@Input()`** | Parent → Child | `<app-card [card]="card" />` |
| **`@Output()` + `EventEmitter`** | Child → Parent | `(delete)="onCardDeleted($event)"` |
| **Route parameters** | Navigate with data | `router.navigate(['/cards', cardId])` |
| **State management** | Complex app-wide state | Signals (your project), NgRx, Akita |

In your project, `AuthService` uses signals for shared state:
```typescript
// Shared via inject() in any component
const authService = inject(AuthService);
const user = authService.currentUser();
const token = authService.token();
```

---

## Intermediate

### 1. How does change detection work? What is `OnPush`?

**Change Detection (CD)** is how Angular knows when to update the DOM. It runs automatically when:
- Events fire (click, submit, keyup)
- HTTP requests complete
- Timers fire (`setTimeout`, `setInterval`)
- RxJS emits (via zone.js interception)

Angular uses `zone.js` to monkey-patch async APIs so it knows when to check for changes.

**Default vs OnPush:**

| Strategy | How it works | When to use |
|----------|-------------|-------------|
| **Default** | Checks every component on every async event | Small apps, simple state |
| **OnPush** | Only checks when `@Input()` reference changes, events fire, or signals update | Large apps, performance-critical |

In Angular 17+, **signals automatically trigger CD** — you don't need `OnPush` or `markForCheck()`. When `signal.set()` is called, Angular knows exactly which parts of the DOM need updating.

```typescript
// Default: Angular checks all components
// OnPush: Angular only checks this component when inputs change
@Component({ changeDetection: ChangeDetectionStrategy.OnPush })

// With signals: CD is automatic, no OnPush needed
isLoading = signal(true);
isLoading.set(false);  // CD runs automatically for this component
```

---

### 2. How do you handle HTTP errors globally?

Two approaches:

**Approach 1: HTTP Interceptor** (used in your project)
```typescript
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401) {
        // Token expired — logout
        authService.logout();
      }
      return throwError(() => error);
    })
  );
};
```

**Approach 2: Global ErrorHandler**
```typescript
@Injectable({ providedIn: 'root' })
export class GlobalErrorHandler implements ErrorHandler {
  handleError(error: any): void {
    console.error('Global error:', error);
    // Log to monitoring service, show user notification, etc.
  }
}
```

---

### 3. What is the purpose of an interceptor?

HTTP interceptors sit between your app and the backend. They can:
- **Add headers** (JWT token, API key, correlation ID)
- **Transform requests** (add base URL, serialize data)
- **Handle errors globally** (401 logout, retry logic)
- **Add caching** (cache GET requests)
- **Log requests/responses** (debugging, analytics)

In your project, the interceptor adds the JWT Bearer token to every request:
```typescript
if (token) {
  req = req.clone({
    setHeaders: { Authorization: `Bearer ${token}` }
  });
}
```

---

### 4. How does lazy loading work?

Instead of loading all code upfront, lazy loading splits your app into chunks that load on demand.

Your project uses `loadComponent` for every route:
```typescript
{
  path: 'dashboard',
  loadComponent: () => import('./features/dashboard/dashboard.component')
    .then(m => m.DashboardComponent)
}
```

**What happens:**
1. User navigates to `/dashboard`
2. Angular dynamically imports the chunk
3. Component is created and rendered
4. Code was NOT loaded at initial page load

**Benefits:** Faster initial load, smaller bundle size, better performance on slow networks.

---

### 5. Difference between `@Input()` and signals for component communication?

| Aspect | `@Input()` | Signals |
|--------|-----------|---------|
| Direction | Parent → Child only | Any direction (via shared service) |
| Reactivity | Requires `OnPush` or `ngOnChanges` | Automatic CD triggering |
| Cross-component | No — only parent/child | Yes — any component that injects the service |
| Type safety | Strong | Strong |
| Your project | Not used | Used everywhere (`currentUser`, `token`, `cards`, etc.) |

```typescript
// @Input() — parent passes data down
@Component({ ... })
class CardComponent {
  @Input() card!: CreditCard;  // Parent must pass [card]
}

// Signal — any component can read/write
@Injectable({ providedIn: 'root' })
class AuthService {
  currentUser = signal<User | null>(null);
}
// Any component: const user = inject(AuthService).currentUser();
```

---

### 6. How do you create and use a custom pipe?

```typescript
// 1. Create the pipe
@Pipe({ name: 'istDate', standalone: true })
export class IstDatePipe implements PipeTransform {
  transform(value: DateInput, pattern = 'MMM dd, yyyy', fallback = '-'): string {
    return formatIstDate(value, pattern, fallback);
  }
}

// 2. Import it in the component that needs it
@Component({
  imports: [IstDatePipe],
  template: `{{ transaction.dateUtc | istDate }}`
})
```

Pipes are **pure** by default — they only recalculate when the input value changes. Good for formatting dates, currencies, text.

---

### 7. What is the difference between `HttpClient` and `fetch` API?

| Aspect | `HttpClient` | `fetch` |
|--------|-------------|---------|
| Return type | `Observable<T>` | `Promise<Response>` |
| Auto JSON parsing | Yes | Manual (`res.json()`) |
| Interceptors | Built-in support | No (need wrappers) |
| Typed responses | `http.get<User>(url)` | Manual type casting |
| Error handling | Via RxJS `catchError` | Check `res.ok` manually |
| Progress tracking | Yes (upload/download) | Limited |
| Testing | `HttpTestingController` | Mock `fetch` globally |
| Your project | Used everywhere | Not used |

```typescript
// HttpClient — your project approach
http.get<ApiResponse<User>>(url).subscribe({
  next: res => console.log(res.data),
  error: err => console.error(err)
});

// fetch — raw browser API
const res = await fetch(url);
const data = await res.json();
if (!res.ok) throw new Error('Failed');
```

---

### 8. How do you optimize Angular app performance?

| Technique | Impact | Your Project |
|-----------|--------|-------------|
| Lazy loading | Faster initial load | All routes use `loadComponent` |
| Standalone components | Smaller bundles | All components are standalone |
| Signals | Automatic, fine-grained CD | Auth state, UI state |
| RxJS `catchError` | Prevents crashes on failed API calls | All HTTP calls wrapped |
| `track` in `@for` | Efficient list rendering | Could be added to transaction list |
| Tree-shakable providers | Unused code removed | `providedIn: 'root'` |
| Deferrable views | Load heavy components on demand | Could add `@defer` for charts |
| Image optimization | Faster page load | Use `NgOptimizedImage` directive |
| Preloading | Background-load lazy chunks | Could add `PreloadAllModules` |

---

## Advanced

### 1. How do signals change change detection?

Before signals: Angular checked the **entire component tree** on every async event. You had to use `OnPush` + `markForCheck()` to optimize.

With signals: Angular knows **exactly which DOM nodes** depend on which signals. Only those nodes update — no component tree traversal needed.

```typescript
// Before signals — Angular checks EVERY component
this.isLoading = true;  // CD runs for all components

// With signals — Angular updates only what uses this signal
isLoading = signal(true);
isLoading.set(false);  // Only components reading isLoading() update
```

Signals also work outside zone.js — they can eventually eliminate zone.js entirely (zoneless Angular).

---

### 2. What is the difference between `BehaviorSubject` and `Signal`?

| Aspect | `BehaviorSubject` | `Signal` |
|--------|------------------|---------|
| API | RxJS stream | Simple getter/setter |
| Subscription | Must subscribe + unsubscribe | No subscription needed — just read |
| Memory leaks | Possible if not unsubscribed | Impossible — no subscriptions |
| Operators | `map`, `filter`, `switchMap`, etc. | Computed signals, effects |
| Timing | Async by default | Synchronous read |
| Interop | Can convert to signal via `toSignal()` | Can convert to observable via `toObservable()` |
| Best for | Streams of events (clicks, HTTP, timers) | State that components read synchronously |

```typescript
// BehaviorSubject — requires subscription
userService.user$.subscribe(user => { ... });

// Signal — just read it
const user = userService.currentUser();
```

**Rule of thumb:** Use signals for state, use observables for async streams.

---

### 3. Explain the Angular rendering pipeline

```
User Action (click, HTTP, timer)
         ↓
   zone.js intercepts
         ↓
   Change Detection runs
         ↓
   Template re-evaluates
         ↓
   DOM updates (minimal diff)
```

**zone.js** monkey-patches all browser async APIs (`setTimeout`, `addEventListener`, `Promise`, `fetch`, `XMLHttpRequest`). When any of these complete, zone.js tells Angular "something changed, run CD."

**Signal-based rendering (future):**
```
signal.set(newValue)
         ↓
   Angular knows which DOM nodes use this signal
         ↓
   Only those nodes update (no CD traversal)
         ↓
   zone.js eventually not needed
```

This is why Angular is moving toward **zoneless** — signals make zone.js unnecessary.

---

### 4. How do you handle memory leaks in Angular?

| Pattern | How it works | Example |
|---------|-------------|---------|
| `takeUntilDestroyed` | Auto-unsubscribes when component is destroyed | `http.get(url).pipe(takeUntilDestroyed()).subscribe()` |
| `DestroyRef` | Register cleanup callbacks | `inject(DestroyRef).onDestroy(() => cleanup())` |
| `async` pipe | Auto-subscribes and unsubscribes | `data$ \| async` |
| Signal cleanup | No subscriptions = no leaks | `effect(() => ..., { allowSignalWrites: true })` |
| Avoid `subscribe` in components | Prefer signals or `async` pipe | Use service signals instead |

In your project, the `forkJoin` in `DashboardComponent` subscribes but doesn't explicitly unsubscribe — in production, add `takeUntilDestroyed()`:
```typescript
forkJoin({ cards: cards$, transactions: transactions$ }).pipe(
  takeUntilDestroyed()  // Auto-unsubscribes on component destroy
).subscribe({ ... });
```

---

### 5. What is zoneless Angular?

Zoneless Angular removes `zone.js` entirely. Instead of relying on zone.js to detect when to run CD, Angular uses **signals and explicit triggers**.

**Why it matters:**
- Smaller bundle (zone.js is ~30KB)
- Faster startup (no monkey-patching)
- Predictable performance (CD only when you want it)
- Better SSR support (zone.js conflicts with Node.js APIs)

Angular 17+ supports zoneless as experimental. Angular 18+ makes it more stable. Your project uses zone.js (via `bootstrapApplication`) but signals already prepare you for zoneless migration.

---

### 6. How would you architect a large-scale Angular app?

```
src/
├── app/
│   ├── core/              # Singleton services (auth, config, interceptors)
│   ├── shared/            # Reusable components, pipes, directives
│   ├── features/          # Feature modules (lazy-loaded)
│   │   ├── auth/
│   │   ├── dashboard/
│   │   ├── billing/
│   │   └── admin/
│   └── app.routes.ts      # Route definitions
├── assets/                # Static files
└── environments/          # Environment configs
```

**Key principles:**
- **Feature-based organization** (not type-based) — group by business domain
- **Smart/Dumb components** — smart components handle data, dumb components handle UI
- **State in services** — use signals/NgRx for shared state
- **Lazy load everything** — no eager-loaded features except `core`
- **Strict TypeScript** — `strict: true`, no `any`, explicit types
- **Testing strategy** — unit test services, component tests with TestBed, E2E with Cypress/Playwright

Your project already follows this architecture.

---

### 7. How do you handle authentication and authorization in Angular?

Three layers:

| Layer | Mechanism | Your Project |
|-------|-----------|-------------|
| **Authentication** | JWT token in `sessionStorage` + signals | `AuthService.token()` |
| **Authorization** | Route guards (`authGuard`, `adminGuard`) | Functional `CanActivateFn` |
| **API Security** | HTTP interceptor adds Bearer token | `authInterceptor` |

**Flow:**
1. User logs in → `AuthService` stores token in `sessionStorage`
2. Every request → `authInterceptor` adds `Authorization: Bearer <token>`
3. Route navigation → `authGuard` checks if token exists
4. Admin route → `adminGuard` checks `currentUser.role === 'admin'`
5. 401 response → interceptor logs user out automatically

**Why sessionStorage over localStorage:** Token is cleared on tab close — more secure, prevents stale tokens after logout.

---

### 8. What is the difference between `forkJoin`, `combineLatest`, and `merge`?

| Operator | When it emits | Use Case |
|----------|--------------|----------|
| **`forkJoin`** | When ALL observables complete | Load multiple independent API calls at once |
| **`combineLatest`** | When ANY observable emits (after all have emitted once) | Combine live data streams (e.g., search + filters) |
| **`merge`** | When ANY observable emits immediately | Multiple event streams into one |

In your project, `forkJoin` loads cards and transactions simultaneously:
```typescript
forkJoin({
  cards: cards$,
  transactions: transactions$
}).subscribe({
  next: res => {
    this.cards.set(res.cards.data || []);
    this.transactions.set(res.transactions.data || []);
  }
});
```

Both API calls run in parallel. The subscription fires only when **both** complete.

---

### 9. How do you test an Angular component with HTTP calls?

Use `HttpTestingController` to mock HTTP requests:

```typescript
import { TestBed } from '@angular/core/testing';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [AuthService, provideHttpClientTesting()]
    });
    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  it('should login and store token', () => {
    service.login({ email: 'test@test.com', password: 'pass' }).subscribe(res => {
      expect(res.success).toBe(true);
      expect(service.token()).toBeTruthy();
    });

    const req = httpMock.expectOne(/\/auth\/login/);
    req.flush({ success: true, data: { accessToken: 'xyz', user: { id: '1' } } });
    httpMock.verify();  // Ensure no unexpected requests
  });
});
```

---

## Project-Specific

### 1. Why did you use `sessionStorage` instead of `localStorage` for tokens?

| Aspect | `sessionStorage` | `localStorage` |
|--------|-----------------|----------------|
| Lifetime | Cleared when tab closes | Persists until explicitly cleared |
| Security | Better — no stale tokens after tab close | Risk — token persists across sessions |
| Multi-tab | Isolated per tab | Shared across all tabs |
| Use case | Banking, financial apps | Apps that want "remember me" |

**For CredVault (credit card management):** Security > convenience. If a user closes the tab, they must re-authenticate. No risk of someone reusing a stale token.

---

### 2. How does your auth interceptor handle 401 errors?

```typescript
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const token = authService.token();
  if (token) {
    req = req.clone({ setHeaders: { Authorization: `Bearer ${token}` } });
  }
  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401 && !isLoggingOut) {
        isLoggingOut = true;  // Prevent infinite logout loop
        authService.logout();
      }
      return throwError(() => error);
    })
  );
};
```

**How it works:**
1. Every HTTP request goes through the interceptor
2. If a token exists, it's added as `Authorization: Bearer <token>`
3. If the server returns 401 (unauthorized/expired token):
   - `isLoggingOut` flag prevents recursive logout calls
   - `authService.logout()` clears signals and sessionStorage
   - Router navigates to `/login`
   - Error is re-thrown so component-level handlers can also respond

---

### 3. Why functional guards vs class-based guards?

| Aspect | Functional Guards | Class Guards |
|--------|------------------|--------------|
| Syntax | `const authGuard: CanActivateFn = () => { ... }` | `class AuthGuard implements CanActivate { ... }` |
| DI | `inject()` function | Constructor injection |
| Boilerplate | Minimal | Requires class, implements, constructor |
| Tree-shaking | Better | Slightly worse |
| Angular version | 14+ | All versions |
| Your project | All 3 guards are functional | Not used |

```typescript
// Functional — your project approach
export const authGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);
  return authService.token() ? true : router.parseUrl('/login');
};

// Class-based — old approach
@Injectable({ providedIn: 'root' })
class AuthGuard implements CanActivate {
  constructor(private authService: AuthService, private router: Router) {}
  canActivate(): boolean {
    return this.authService.token() !== null;
  }
}
```

Functional guards are simpler, more testable, and align with the standalone/components-without-modules direction.

---

### 4. How does your multi-step card validation work?

Three-step process in `DashboardComponent`:

```
Step 1: Fill details → Step 2: Review → Step 3: Submit
```

**Step 1 — Validation before proceeding to review:**
```typescript
goToReviewStep(): void {
  // Mark all fields as touched to show errors
  cardholderNameCtrl?.markAsTouched();
  cardNumberCtrl?.markAsTouched();
  
  // Validate card number is exactly 16 digits
  if (normalizedCardNumber.length !== 16) {
    this.errorMessage.set('Card number must contain exactly 16 digits.');
    return;
  }
  
  // Detect card network from number (Visa starts with 4, MC with 51-55 or 2221-2720)
  const detected = this.getDetectedCardNetwork(cardNumber);
  
  // Validate network matches selected issuer
  if (detected !== issuerNetwork) {
    this.errorMessage.set(`Card looks like ${detected}, but issuer is ${issuerNetwork}`);
    return;
  }
  
  // If all valid, advance to step 2
  this.addCardStep.set(2);
}
```

**Step 2 — Review (displays masked card number, issuer, expiry):**
- Shows `•••• •••• •••• 1234` format
- Displays issuer label with network
- User confirms or goes back

**Step 3 — Submit:**
- Re-validates everything (defense in depth)
- Sends POST to API
- On success: closes modal, reloads dashboard
- On error: shows backend error message

---

### 5. What is `NgZone` and why did you use it for Google OAuth?

**Problem:** Google Identity Services (GSI) runs its callbacks **outside** Angular's zone. When the callback fires, Angular doesn't know about it, so change detection doesn't run — UI won't update.

**Solution:** Wrap the callback in `NgZone.run()` so Angular knows to trigger CD:

```typescript
google.accounts.id.initialize({
  client_id: this.googleClientId,
  callback: (response) => {
    this.zone.run(() => this.handleGoogleCredential(response.credential));
  }
});
```

**Without `NgZone.run()`:** User clicks Google button → login succeeds → token is stored → but UI still shows login form (no CD triggered).

**With `NgZone.run()`:** User clicks Google button → login succeeds → Angular detects the change → router navigates to dashboard → UI updates correctly.

**Alternative (Angular 17+):** Use signals with `effect()` or zoneless approach — but for now, `NgZone` is the cleanest fix for third-party library callbacks.
