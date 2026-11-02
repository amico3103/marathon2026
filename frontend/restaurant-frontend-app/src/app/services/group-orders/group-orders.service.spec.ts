import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AuthenticationService } from '@app/auth/services/authentication.service';
import { SKIP_ERROR_TOAST } from '@app/auth/skip-error-toast.context';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { GroupOrdersService } from './group-orders.service';

describe('GroupOrdersService', () => {
  let service: GroupOrdersService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: 'https://api.example' },
        {
          provide: AuthenticationService,
          useValue: { getTokens: () => ({ idToken: 'owner-token', accessToken: 'access' }) },
        },
      ],
    });
    service = TestBed.inject(GroupOrdersService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('sets an absolute dish quantity using the owner token, with zero representing removal', () => {
    service.updateDish$('group-1', 'guest-1', 'dish-1', 0).subscribe();
    const request = http.expectOne('https://api.example/guest/groups/group-1/sessions/guest-1');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ dishId: 'dish-1', quantity: 0 });
    expect(request.request.headers.get('Authorization')).toBe('Bearer owner-token');
    request.flush({ participants: [], total: 0 });
  });

  it('deletes the selected guest with the owner token and returns the server snapshot', () => {
    const snapshot = { participants: [], total: 0 };
    service.removeSession$('group/id', 'guest/id').subscribe(result => expect(result).toEqual(snapshot));
    const request = http.expectOne('https://api.example/guest/groups/group%2Fid/sessions/guest%2Fid');
    expect(request.request.method).toBe('DELETE');
    expect(request.request.headers.get('Authorization')).toBe('Bearer owner-token');
    request.flush(snapshot);
  });

  it('loads owner-visible sessions with the owner token and silent context', () => {
    service.getSessions$('group/id', { silent: true }).subscribe();

    const request = http.expectOne('https://api.example/guest/groups/group%2Fid/sessions');
    expect(request.request.method).toBe('GET');
    expect(request.request.headers.get('Authorization')).toBe('Bearer owner-token');
    expect(request.request.context.get(SKIP_ERROR_TOAST)).toBeTrue();
    request.flush({ participants: [], total: 0 });
  });

  it('loads the live group status', () => {
    service.getStatus$('group-1').subscribe();

    const request = http.expectOne('https://api.example/guest/groups/group-1/status');
    expect(request.request.method).toBe('GET');
    request.flush({ currentParticipants: 2, maxCapacity: 4, status: 'Open' });
  });

  it('marks background status and owner synchronization requests as silent', () => {
    service.getStatus$('group-1', { silent: true }).subscribe();
    service.getOwnerSessionId$('reservation-1', { silent: true }).subscribe();
    const status = http.expectOne('https://api.example/guest/groups/group-1/status');
    const owner = http.expectOne('https://api.example/reservations/reservation-1/guest-session-id');
    expect(status.request.context.get(SKIP_ERROR_TOAST)).toBeTrue();
    expect(owner.request.context.get(SKIP_ERROR_TOAST)).toBeTrue();
    status.flush({ currentParticipants: 2, maxCapacity: 4, status: 'Open' });
    owner.flush({ guestSessionId: 'owner-session', groupReservationId: 'group-1' });
  });

  it('loads the owner session id', () => {
    service.getOwnerSessionId$('reservation-1').subscribe();

    const request = http.expectOne(
      'https://api.example/reservations/reservation-1/guest-session-id',
    );
    expect(request.request.method).toBe('GET');
    request.flush({ guestSessionId: 'owner-session', groupReservationId: 'group-1' });
  });

  it('submits the reservation id in the group request body', () => {
    service.submit$('group-1', 'reservation-1').subscribe();

    const request = http.expectOne('https://api.example/group-reservations/group-1/submit');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ reservationId: 'reservation-1' });
    request.flush({ message: 'ok' });
  });
});
