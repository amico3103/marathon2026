import { HttpErrorResponse } from '@angular/common/http';
import {
  ComponentFixture,
  TestBed,
  discardPeriodicTasks,
  fakeAsync,
  tick,
} from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { GroupSessionsResponse } from '@app/models/group-orders/group-sessions-response.model';
import { GroupOrdersService } from '@app/services/group-orders/group-orders.service';
import { ReservationService } from '@app/services/reservation/reservation.service';
import { ToastService } from '@app/services/toast/toast.service';
import { BehaviorSubject, Observable, Subject, of, throwError } from 'rxjs';
import { GroupOrderPageComponent } from './group-order-page.component';

const sessions: GroupSessionsResponse = {
  total: 760,
  participants: [
    {
      id: 'owner-session',
      type: 'Owner',
      name: 'Olena',
      isSubmitted: true,
      total: 100,
      items: [{ dish: { id: 'soup', name: 'Soup', price: 100 }, quantity: 1 }],
    },
    {
      id: 'guest-session',
      type: 'Guest',
      name: 'Alex',
      isSubmitted: false,
      total: 660,
      items: [{ dish: { id: 'steak', name: 'Steak', price: 330 }, quantity: 2 }],
    },
  ],
};

class GroupOrdersServiceStub {
  readonly status = {
    currentParticipants: 2,
    maxCapacity: 4,
    status: 'Open' as const,
  };
  readonly submitResponse$ = new Subject<unknown>();
  readonly removeResponse$ = new Subject<GroupSessionsResponse>();
  readonly editResponse$ = new Subject<GroupSessionsResponse>();
  sessionsCalls: Array<{ silent?: boolean }> = [];
  submitCalls = 0;

  getSessions$(_id: string, options: { silent?: boolean } = {}): Observable<GroupSessionsResponse> {
    this.sessionsCalls.push(options);
    return of(sessions);
  }

  getStatus$() {
    return of(this.status);
  }

  getOwnerSessionId$(_reservationId?: string, _options?: { silent?: boolean }) {
    return of({ guestSessionId: 'owner-session', groupReservationId: 'group-1' });
  }

  submit$(): Observable<unknown> {
    this.submitCalls += 1;
    return this.submitResponse$;
  }

  removeSession$(_groupId: string, _guestId: string): Observable<GroupSessionsResponse> {
    return this.removeResponse$;
  }

  updateDish$(_groupId: string, _sessionId: string, _dishId: string, _quantity: number): Observable<GroupSessionsResponse> {
    return this.editResponse$;
  }
}

describe('hard owner group pre-order page', () => {
  let fixture: ComponentFixture<GroupOrderPageComponent>;
  let element: HTMLElement;
  let groupOrders: GroupOrdersServiceStub;
  let toast: jasmine.SpyObj<ToastService>;
  let routeParams: BehaviorSubject<ReturnType<typeof convertToParamMap>>;

  beforeEach(() => {
    routeParams = new BehaviorSubject(convertToParamMap({ reservationId: 'reservation-1' }));
    toast = jasmine.createSpyObj<ToastService>('ToastService', ['success']);
    TestBed.configureTestingModule({
      imports: [GroupOrderPageComponent],
      providers: [
        {
          provide: ActivatedRoute,
          useValue: { paramMap: routeParams.asObservable() },
        },
        {
          provide: ReservationService,
          useValue: {
            getReservationById: () =>
              of({
                id: 'reservation-1',
                tableId: 'table-1',
                startTime: '2026-10-29T18:00:00Z',
                endTime: '2026-10-29T20:00:00Z',
                guestCount: 4,
                status: 0,
                restaurantName: 'Green & Tasty',
                locationAddress: 'Main Street 1',
                waiterId: 'waiter-1',
              }),
          },
        },
        { provide: GroupOrdersService, useClass: GroupOrdersServiceStub },
        { provide: ToastService, useValue: toast },
      ],
    });
    fixture = TestBed.createComponent(GroupOrderPageComponent);
    groupOrders = TestBed.inject(GroupOrdersService) as unknown as GroupOrdersServiceStub;
    fixture.detectChanges();
    element = fixture.nativeElement as HTMLElement;
  });

  afterEach(() => fixture.destroy());

  it('recalculates submitted totals from the edited server snapshot and blocks parallel mutations', () => {
    const edit = spyOn(groupOrders, 'updateDish$').and.callThrough();
    const change = { participantId: 'owner-session', dishId: 'soup', quantity: 3 };
    fixture.componentInstance.updateDish(change);
    fixture.componentInstance.updateDish(change);
    fixture.componentInstance.submitPreOrder();
    expect(edit).toHaveBeenCalledOnceWith('group-1', 'owner-session', 'soup', 3);
    expect(groupOrders.submitCalls).toBe(0);
    groupOrders.editResponse$.next({ participants: [{ ...sessions.participants[0], total: 300,
      items: [{ dish: { id: 'soup', name: 'Soup', price: 100 }, quantity: 3 }] }], total: 300 });
    groupOrders.editResponse$.complete();
    expect(fixture.componentInstance.grandTotal()).toBe(300);
    expect(fixture.componentInstance.updatingDish()).toBeFalse();
  });

  it('retains quantities when edit fails and permits retry', () => {
    const edit = spyOn(groupOrders, 'updateDish$').and.returnValue(throwError(() => new HttpErrorResponse({ status: 409 })));
    const change = { participantId: 'guest-session', dishId: 'steak', quantity: 0 };
    fixture.componentInstance.updateDish(change);
    expect(fixture.componentInstance.participants()).toEqual(sessions.participants);
    expect(fixture.componentInstance.updatingDish()).toBeFalse();
    edit.and.returnValue(groupOrders.editResponse$);
    fixture.componentInstance.updateDish(change);
    expect(edit).toHaveBeenCalledTimes(2);
    groupOrders.editResponse$.next({ participants: [sessions.participants[0]], total: 100 });
    groupOrders.editResponse$.complete();
    expect(fixture.componentInstance.grandTotal()).toBe(100);
  });

  it('loads the new reservation when the same route component receives another id', () => {
    const owner = spyOn(groupOrders, 'getOwnerSessionId$').and.returnValue(
      of({ guestSessionId: 'owner-2', groupReservationId: 'group-2' }),
    );
    const read = spyOn(groupOrders, 'getSessions$').and.returnValue(of({ participants: [], total: 0 }));
    routeParams.next(convertToParamMap({ reservationId: 'reservation-2' }));
    fixture.detectChanges();
    expect(owner).toHaveBeenCalledWith('reservation-2');
    expect(read).toHaveBeenCalledWith('group-2', { silent: false });
    expect(fixture.componentInstance.reservationId).toBe('reservation-2');
    expect(fixture.componentInstance.ownerSessionId()).toBe('owner-2');
    expect(element.textContent).not.toContain('Guest: Alex');
    expect(element.textContent).toContain("Participants haven't added any dishes yet");
    expect(element.querySelector<HTMLButtonElement>('.submit-button')?.disabled).toBeTrue();
  });

  it('ignores a delayed initial response for the previous reservation', () => {
    const delayed = new Subject<{ guestSessionId: string; groupReservationId: string }>();
    const owner = spyOn(groupOrders, 'getOwnerSessionId$').and.returnValue(delayed);
    routeParams.next(convertToParamMap({ reservationId: 'reservation-2' }));
    fixture.detectChanges();
    expect(element.textContent).toContain('Loading the latest pre-order data');
    expect(element.querySelector('.participants')).toBeNull();
    owner.and.returnValue(of({ guestSessionId: 'owner-3', groupReservationId: 'group-3' }));
    routeParams.next(convertToParamMap({ reservationId: 'reservation-3' }));
    delayed.next({ guestSessionId: 'stale-owner', groupReservationId: 'stale-group' });
    delayed.complete();
    expect(fixture.componentInstance.groupReservationId()).toBe('group-3');
    expect(fixture.componentInstance.ownerSessionId()).toBe('owner-3');
  });

  for (const status of [404, 503]) {
    it(`shows the initial ${status} error and recovers through Try again`, () => {
      const owner = spyOn(groupOrders, 'getOwnerSessionId$').and.returnValue(
        throwError(() => new HttpErrorResponse({ status })),
      );
      routeParams.next(convertToParamMap({ reservationId: 'reservation-2' }));
      fixture.detectChanges();
      expect(element.textContent).toContain(status === 404
        ? 'Group reservation not found' : 'Could not load the group pre-order');
      owner.and.returnValue(of({ guestSessionId: 'owner-session', groupReservationId: 'group-1' }));
      const retry = [...element.querySelectorAll<HTMLButtonElement>('button')]
        .find(button => button.textContent?.trim() === 'Try again')!;
      retry.click();
      fixture.detectChanges();
      expect(element.textContent).toContain('Guest: Alex');
    });
  }

  it('keeps one polling timer after repeated reloads and stops requests on destroy', fakeAsync(() => {
    const read = spyOn(groupOrders, 'getSessions$').and.callThrough();
    fixture.componentInstance.load();
    fixture.componentInstance.load();
    read.calls.reset();
    tick(15_000);
    expect(read).toHaveBeenCalledTimes(1);
    fixture.destroy();
    tick(30_000);
    expect(read).toHaveBeenCalledTimes(1);
    discardPeriodicTasks();
  }));

  it('does not overlap polling and manual refresh while the previous response is pending', fakeAsync(() => {
    fixture.componentInstance.load();
    const delayed = new Subject<GroupSessionsResponse>();
    const read = spyOn(groupOrders, 'getSessions$').and.returnValue(delayed);
    tick(15_000);
    fixture.componentInstance.refreshNow();
    tick(30_000);
    expect(read).toHaveBeenCalledTimes(1);
    delayed.next(sessions);
    delayed.complete();
    fixture.destroy();
    discardPeriodicTasks();
  }));

  it('cancels a pending refresh when navigating to another reservation', () => {
    const delayed = new Subject<GroupSessionsResponse>();
    const read = spyOn(groupOrders, 'getSessions$').and.returnValue(delayed);
    fixture.componentInstance.refreshNow();
    read.and.returnValue(of({ participants: [], total: 0 }));
    routeParams.next(convertToParamMap({ reservationId: 'reservation-2' }));
    delayed.next(sessions);
    delayed.complete();
    expect(fixture.componentInstance.participants()).toEqual([]);
    expect(fixture.componentInstance.refreshing()).toBeFalse();
  });

  it('confirms the guest name and allows Cancel without deleting', () => {
    const remove = spyOn(groupOrders, 'removeSession$').and.callThrough();
    element.querySelector<HTMLButtonElement>('[aria-label="Remove Alex"]')!.click();
    fixture.detectChanges();
    expect(element.querySelector('[role="dialog"]')?.textContent).toContain('Alex');
    expect(element.querySelector('[role="dialog"]')?.textContent).toContain('This action cannot be undone.');
    [...element.querySelectorAll<HTMLButtonElement>('[modal-actions]')]
      .find(button => button.textContent?.trim() === 'Cancel')!.click();
    expect(fixture.componentInstance.guestToRemove()).toBeNull();
    expect(remove).not.toHaveBeenCalled();
  });

  it('uses the DELETE snapshot to recompute participants and total and prevents double deletion', () => {
    const remove = spyOn(groupOrders, 'removeSession$').and.callThrough();
    fixture.componentInstance.openRemoveConfirmation(sessions.participants[1]);
    fixture.componentInstance.removeGuest();
    fixture.componentInstance.removeGuest();
    fixture.componentInstance.submitPreOrder();
    expect(remove).toHaveBeenCalledOnceWith('group-1', 'guest-session');
    expect(groupOrders.submitCalls).toBe(0);
    expect(fixture.componentInstance.removing()).toBeTrue();
    const owner = { ...sessions.participants[0], total: 200,
      items: [{ dish: { id: 'soup', name: 'Soup', price: 100 }, quantity: 2 }] };
    groupOrders.removeResponse$.next({ participants: [owner], total: 200 });
    groupOrders.removeResponse$.complete();
    fixture.detectChanges();
    expect(fixture.componentInstance.grandTotal()).toBe(200);
    expect(fixture.componentInstance.pendingCount()).toBe(0);
    expect(element.textContent).not.toContain('Guest: Alex');
    expect(element.textContent).toContain('1 of 4 guests');
    expect(element.querySelector('[role="dialog"]')).toBeNull();
  });

  it('does not offer removal for Owner or a submitted group', () => {
    expect(element.querySelector('[aria-label="Remove Olena"]')).toBeNull();
    fixture.componentInstance.openRemoveConfirmation(sessions.participants[0]);
    expect(fixture.componentInstance.guestToRemove()).toBeNull();
    fixture.componentInstance.groupStatus.set('Submitted');
    fixture.detectChanges();
    expect(element.querySelector('.remove-button')).toBeNull();
    fixture.componentInstance.openRemoveConfirmation(sessions.participants[1]);
    expect(fixture.componentInstance.guestToRemove()).toBeNull();
  });

  it('keeps the guest and permits retry after DELETE fails', () => {
    const remove = spyOn(groupOrders, 'removeSession$').and.returnValue(
      throwError(() => new HttpErrorResponse({ status: 503 })),
    );
    fixture.componentInstance.openRemoveConfirmation(sessions.participants[1]);
    fixture.componentInstance.removeGuest();
    expect(fixture.componentInstance.removing()).toBeFalse();
    expect(fixture.componentInstance.participants()).toEqual(sessions.participants);
    expect(fixture.componentInstance.guestToRemove()?.name).toBe('Alex');
    expect(toast.success).not.toHaveBeenCalled();
    remove.and.returnValue(groupOrders.removeResponse$);
    fixture.componentInstance.removeGuest();
    expect(remove).toHaveBeenCalledTimes(2);
  });

  it('cancels stale refreshes before deleting and resumes polling afterwards', fakeAsync(() => {
    fixture.componentInstance['stopPolling']();
    const delayedCarts = new Subject<GroupSessionsResponse>();
    const read = spyOn(groupOrders, 'getSessions$').and.returnValue(delayedCarts);
    fixture.componentInstance.refreshNow();
    fixture.componentInstance.openRemoveConfirmation(sessions.participants[1]);
    fixture.componentInstance.removeGuest();
    fixture.componentInstance.refreshNow();
    tick(30_000);
    expect(read).toHaveBeenCalledTimes(1);
    groupOrders.removeResponse$.next({ participants: [sessions.participants[0]], total: 100 });
    groupOrders.removeResponse$.complete();
    delayedCarts.next(sessions);
    delayedCarts.complete();
    expect(fixture.componentInstance.participants().length).toBe(1);
    read.and.returnValue(of({ participants: [sessions.participants[0]], total: 100 }));
    tick(15_000);
    expect(read).toHaveBeenCalledTimes(2);
    fixture.destroy();
    discardPeriodicTasks();
  }));

  it('renders live reservation details and identifies the owner cart', () => {
    const text = element.textContent ?? '';
    expect(text).toContain('Main Street 1');
    expect(text).toContain('2 of 4 guests');
    expect(text).toContain('My Pre-Order');
    expect(text).toContain('Guest: Alex');
  });

  it('consolidates only submitted carts instead of trusting the API total', () => {
    const text = element.textContent ?? '';
    expect(text).toContain('Grand Total');
    expect(text).toContain('$100.00');
    expect(text).not.toContain('$760.00');
    expect(text).toContain("1 participant hasn't submitted their cart yet");
  });

  it('opens confirmation and prevents duplicate submit requests', () => {
    element.querySelector<HTMLButtonElement>('.submit-button')!.click();
    fixture.detectChanges();
    const submit = [...element.querySelectorAll<HTMLButtonElement>('[modal-actions]')].find(
      (button) => button.textContent?.trim() === 'Submit',
    )!;
    submit.click();
    submit.click();

    expect(groupOrders.submitCalls).toBe(1);
  });

  it('switches to read-only state after submit succeeds', () => {
    element.querySelector<HTMLButtonElement>('.submit-button')!.click();
    fixture.detectChanges();
    [...element.querySelectorAll<HTMLButtonElement>('[modal-actions]')]
      .find((button) => button.textContent?.trim() === 'Submit')!
      .click();
    groupOrders.submitResponse$.next({});
    groupOrders.submitResponse$.complete();
    fixture.detectChanges();

    expect(element.textContent).toContain('Sent to Restaurant');
    expect(element.querySelector<HTMLButtonElement>('.submit-button')?.disabled).toBeTrue();
    expect(toast.success).toHaveBeenCalledWith('Pre-order sent to the restaurant');
  });

  it('refreshes silently after 15 seconds', fakeAsync(() => {
    fixture.componentInstance['stopPolling']();
    fixture.componentInstance['startPolling']();
    tick(15_000);
    fixture.detectChanges();

    expect(groupOrders.sessionsCalls.some((options) => options.silent)).toBeTrue();
    discardPeriodicTasks();
  }));

  it('keeps the last data and reports a failed background refresh', fakeAsync(() => {
    groupOrders.getSessions$ = () =>
      throwError(() => new HttpErrorResponse({ status: 503 }));
    fixture.componentInstance['stopPolling']();
    fixture.componentInstance['startPolling']();
    tick(15_000);
    fixture.detectChanges();

    expect(element.textContent).toContain("Couldn't refresh. Showing data from");
    expect(element.textContent).toContain('Main Street 1');
    discardPeriodicTasks();
  }));

  it('waits for owner synchronization before reading refreshed carts', () => {
    const ownerResponse = new Subject<{ guestSessionId: string; groupReservationId: string }>();
    const synchronize = spyOn(groupOrders, 'getOwnerSessionId$').and.returnValue(ownerResponse);
    const readCarts = spyOn(groupOrders, 'getSessions$').and.callThrough();

    fixture.componentInstance.refreshNow();
    expect(synchronize).toHaveBeenCalledWith('reservation-1', { silent: false });
    expect(readCarts).not.toHaveBeenCalled();
    expect(fixture.componentInstance.refreshing()).toBeTrue();

    ownerResponse.next({ guestSessionId: 'owner-session', groupReservationId: 'group-1' });
    ownerResponse.complete();
    expect(readCarts).toHaveBeenCalledTimes(1);
    expect(fixture.componentInstance.refreshing()).toBeFalse();
  });

  for (const trigger of ['manual', 'polling'] as const) {
    it(`updates owner dishes, quantities and submitted state through ${trigger} refresh`, fakeAsync(() => {
      fixture.componentInstance['stopPolling']();
      let synchronized = false;
      spyOn(groupOrders, 'getOwnerSessionId$').and.callFake(() => {
        synchronized = true;
        return of({ guestSessionId: 'owner-session', groupReservationId: 'group-1' });
      });
      spyOn(groupOrders, 'getSessions$').and.callFake(() => {
        expect(synchronized).toBeTrue();
        return of({
          total: 90,
          participants: [{
            ...sessions.participants[0],
            isSubmitted: false,
            total: 90,
            items: [{ dish: { id: 'salad', name: 'Fresh salad', price: 30 }, quantity: 3 }],
          }],
        });
      });

      if (trigger === 'polling') {
        fixture.componentInstance['startPolling']();
        tick(15_000);
      } else {
        fixture.componentInstance.refreshNow();
      }
      fixture.detectChanges();

      expect(element.textContent).toContain('Fresh salad');
      expect(element.querySelector('.dish-controls span')?.textContent).toBe('3');
      expect(fixture.componentInstance.participants()[0].isSubmitted).toBeFalse();
      expect(fixture.componentInstance.grandTotal()).toBe(0);
      fixture.destroy();
      discardPeriodicTasks();
    }));
  }

  it('retains the last snapshot when owner synchronization fails and allows retry', () => {
    const timestamp = fixture.componentInstance.updatedAt();
    const synchronize = spyOn(groupOrders, 'getOwnerSessionId$').and.returnValue(
      throwError(() => new HttpErrorResponse({ status: 503 })),
    );
    const readCarts = spyOn(groupOrders, 'getSessions$').and.callThrough();
    fixture.componentInstance.refreshNow();

    expect(readCarts).not.toHaveBeenCalled();
    expect(fixture.componentInstance.participants()).toEqual(sessions.participants);
    expect(fixture.componentInstance.updatedAt()).toBe(timestamp);
    expect(fixture.componentInstance.syncError()).toBeTrue();
    expect(fixture.componentInstance.refreshing()).toBeFalse();

    synchronize.and.returnValue(of({ guestSessionId: 'owner-session', groupReservationId: 'group-1' }));
    fixture.componentInstance.refreshNow();
    expect(readCarts).toHaveBeenCalledTimes(1);
    expect(fixture.componentInstance.syncError()).toBeFalse();
  });

  for (const trigger of ['manual', 'polling'] as const) {
    it(`cancels an in-flight ${trigger} refresh before submit and ignores its stale Open response`, fakeAsync(() => {
      fixture.componentInstance['stopPolling']();
      const delayedCarts = new Subject<GroupSessionsResponse>();
      const readCarts = spyOn(groupOrders, 'getSessions$').and.returnValue(delayedCarts);
      if (trigger === 'polling') {
        fixture.componentInstance['startPolling']();
        tick(15_000);
      } else {
        fixture.componentInstance.refreshNow();
      }
      expect(fixture.componentInstance.refreshing()).toBeTrue();

      fixture.componentInstance.submitPreOrder();
      expect(fixture.componentInstance.refreshing()).toBeFalse();
      fixture.componentInstance.refreshNow();
      tick(30_000);
      expect(readCarts).toHaveBeenCalledTimes(1);

      groupOrders.submitResponse$.next({});
      groupOrders.submitResponse$.complete();
      delayedCarts.next({ participants: [], total: 0 });
      delayedCarts.complete();
      fixture.detectChanges();

      expect(fixture.componentInstance.groupStatus()).toBe('Submitted');
      expect(fixture.componentInstance.participants()).toEqual(sessions.participants);
      expect(element.textContent).toContain('Sent to Restaurant');
      expect(element.querySelector<HTMLButtonElement>('.submit-button')?.disabled).toBeTrue();
      tick(30_000);
      expect(readCarts).toHaveBeenCalledTimes(1);
      fixture.destroy();
      discardPeriodicTasks();
    }));
  }

  it('resumes polling and permits another submit after a failed submit', fakeAsync(() => {
    fixture.componentInstance['stopPolling']();
    const readCarts = spyOn(groupOrders, 'getSessions$').and.callThrough();
    const submit = spyOn(groupOrders, 'submit$').and.returnValue(
      throwError(() => new HttpErrorResponse({ status: 400 })),
    );
    fixture.componentInstance.submitPreOrder();
    expect(fixture.componentInstance.submitting()).toBeFalse();
    expect(fixture.componentInstance.canSubmit()).toBeTrue();
    tick(15_000);
    expect(readCarts).toHaveBeenCalledTimes(1);

    submit.and.returnValue(groupOrders.submitResponse$);
    fixture.componentInstance.submitPreOrder();
    groupOrders.submitResponse$.next({});
    groupOrders.submitResponse$.complete();
    expect(fixture.componentInstance.groupStatus()).toBe('Submitted');
    tick(30_000);
    expect(readCarts).toHaveBeenCalledTimes(1);
    fixture.destroy();
    discardPeriodicTasks();
  }));
});
