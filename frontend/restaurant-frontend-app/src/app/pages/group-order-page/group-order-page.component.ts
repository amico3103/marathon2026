import { CurrencyPipe, DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute } from '@angular/router';
import { ParticipantCardComponent } from '@app/components/group-orders/participant-card/participant-card.component';
import {
  GroupReservationStatus,
  GroupReservationStatusResponse,
} from '@app/models/group-orders/group-reservation-status.model';
import { DishQuantityChange, Participant } from '@app/models/group-orders/participant.model';
import { ReservationCustomer } from '@app/models/reservations/reservation.model';
import { GroupOrdersService } from '@app/services/group-orders/group-orders.service';
import { ReservationService } from '@app/services/reservation/reservation.service';
import { ToastService } from '@app/services/toast/toast.service';
import { ModalComponent } from '@app/shared/custom-components/modal/modal.component';
import {
  ConsolidatedPreOrderItem,
  consolidatePreOrder,
} from '@app/shared/helpers/consolidate-pre-order';
import {
  EMPTY,
  Subscription,
  catchError,
  distinctUntilChanged,
  exhaustMap,
  finalize,
  forkJoin,
  interval,
  map,
  switchMap,
  tap,
} from 'rxjs';

type PageState = 'loading' | 'ready' | 'error';

interface GroupSnapshot {
  participants: Participant[];
  status: GroupReservationStatusResponse;
}

@Component({
  standalone: true,
  selector: 'app-group-order-page',
  templateUrl: './group-order-page.component.html',
  styleUrl: './group-order-page.component.scss',
  imports: [CurrencyPipe, DatePipe, ModalComponent, ParticipantCardComponent],
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class GroupOrderPageComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);
  private readonly groupOrdersService = inject(GroupOrdersService);
  private readonly reservationService = inject(ReservationService);
  private readonly toastService = inject(ToastService);
  private pollingSubscription?: Subscription;
  private refreshSubscription?: Subscription;
  private loadSubscription?: Subscription;
  private mutationSubscription?: Subscription;

  reservationId = '';
  readonly pageState = signal<PageState>('loading');
  readonly errorMessage = signal('');
  readonly reservation = signal<ReservationCustomer | null>(null);
  readonly groupReservationId = signal('');
  readonly participants = signal<Participant[]>([]);
  readonly ownerSessionId = signal('');
  readonly groupStatus = signal<GroupReservationStatus>('Open');
  readonly maxCapacity = signal(0);
  readonly updatedAt = signal<Date | null>(null);
  readonly syncError = signal(false);
  readonly refreshing = signal(false);
  readonly submitting = signal(false);
  readonly showSubmitModal = signal(false);
  readonly guestToRemove = signal<Participant | null>(null);
  readonly removing = signal(false);
  readonly updatingDish = signal(false);

  readonly pendingCount = computed(
    () => this.participants().filter((participant) => !participant.isSubmitted).length,
  );
  readonly consolidatedItems = computed<ConsolidatedPreOrderItem[]>(() =>
    consolidatePreOrder(this.participants()),
  );
  readonly grandTotal = computed(() =>
    this.consolidatedItems().reduce((total, item) => total + item.total, 0),
  );
  readonly guestsLabel = computed(() => {
    const capacity = this.maxCapacity() || this.reservation()?.guestCount || 0;
    return `${this.participants().length} of ${capacity} guests`;
  });
  readonly statusLabel = computed(() => {
    switch (this.groupStatus()) {
      case 'Open':
        return 'Collecting pre-orders';
      case 'Submitted':
        return 'Sent to Restaurant';
      case 'Closed':
        return 'Closed';
    }
  });
  readonly canSubmit = computed(
    () =>
      this.groupStatus() === 'Open' &&
      this.consolidatedItems().length > 0 &&
      !this.submitting() &&
      !this.guestToRemove() &&
      !this.removing() && !this.updatingDish(),
  );

  constructor() {
    this.destroyRef.onDestroy(() => this.stopPolling());
    this.route.paramMap.pipe(
      map((params) => params.get('reservationId') ?? ''),
      distinctUntilChanged(),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe((reservationId) => {
      this.reservationId = reservationId;
      this.load();
    });
  }

  load(): void {
    this.stopPolling();
    this.refreshSubscription?.unsubscribe();
    this.loadSubscription?.unsubscribe();
    this.mutationSubscription?.unsubscribe();
    this.reservation.set(null);
    this.groupReservationId.set('');
    this.ownerSessionId.set('');
    this.participants.set([]);
    this.updatedAt.set(null);
    this.groupStatus.set('Open');
    this.showSubmitModal.set(false);
    this.guestToRemove.set(null);
    this.pageState.set('loading');
    this.errorMessage.set('');
    this.syncError.set(false);

    if (!this.reservationId) {
      this.pageState.set('error');
      this.errorMessage.set('Group reservation not found');
      return;
    }

    this.loadSubscription = forkJoin({
      owner: this.groupOrdersService.getOwnerSessionId$(this.reservationId),
      reservation: this.reservationService.getReservationById(this.reservationId),
    })
      .pipe(
        switchMap(({ owner, reservation }) =>
          this.getSnapshot$(owner.groupReservationId, false).pipe(
            map((snapshot) => ({ owner, reservation, snapshot })),
          ),
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: ({ owner, reservation, snapshot }) => {
          this.reservation.set(reservation);
          this.groupReservationId.set(owner.groupReservationId);
          this.ownerSessionId.set(owner.guestSessionId);
          this.applySnapshot(snapshot);
          this.pageState.set('ready');
          this.startPolling();
        },
        error: (error: HttpErrorResponse) => {
          this.pageState.set('error');
          this.errorMessage.set(
            error.status === 404 ? 'Group reservation not found' : 'Could not load the group pre-order',
          );
        },
      });
  }

  refreshNow(): void {
    this.refresh(false);
  }

  openSubmitConfirmation(): void {
    if (this.canSubmit()) {
      this.showSubmitModal.set(true);
    }
  }

  closeSubmitConfirmation(): void {
    if (!this.submitting()) {
      this.showSubmitModal.set(false);
    }
  }

  submitPreOrder(): void {
    const groupReservationId = this.groupReservationId();
    if (!groupReservationId || !this.canSubmit()) {
      return;
    }

    this.submitting.set(true);
    this.stopPolling();
    this.refreshSubscription?.unsubscribe();
    this.mutationSubscription = this.groupOrdersService
      .submit$(groupReservationId, this.reservationId)
      .pipe(
        finalize(() => this.submitting.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: () => {
          this.groupStatus.set('Submitted');
          this.updatedAt.set(new Date());
          this.showSubmitModal.set(false);
          this.stopPolling();
          this.toastService.success('Pre-order sent to the restaurant');
        },
        error: () => {
          // The shared interceptor shows the API error. Keep the dialog actionable for retry.
          this.startPolling();
        },
      });
  }

  openRemoveConfirmation(participant: Participant): void {
    if (this.groupStatus() !== 'Open' || participant.type !== 'Guest' ||
        this.submitting() || this.removing() || this.updatingDish() || this.showSubmitModal()) {
      return;
    }
    this.guestToRemove.set(participant);
  }

  closeRemoveConfirmation(): void {
    if (!this.removing()) {
      this.guestToRemove.set(null);
    }
  }

  removeGuest(): void {
    const guest = this.guestToRemove();
    const groupId = this.groupReservationId();
    if (!guest || !groupId || guest.type !== 'Guest' || this.groupStatus() !== 'Open' ||
        this.removing() || this.submitting() || this.updatingDish()) {
      return;
    }

    this.removing.set(true);
    this.stopPolling();
    this.refreshSubscription?.unsubscribe();
    this.mutationSubscription = this.groupOrdersService.removeSession$(groupId, guest.id)
      .pipe(
        finalize(() => this.removing.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (snapshot) => {
          this.participants.set(snapshot.participants);
          this.updatedAt.set(new Date());
          this.syncError.set(false);
          this.guestToRemove.set(null);
          this.startPolling();
        },
        error: () => {
          // Keep the confirmation open for retry; the interceptor displays the API error.
          this.startPolling();
        },
      });
  }

  private getSnapshot$(groupReservationId: string, silent: boolean) {
    return forkJoin({
      participants: this.groupOrdersService.getSessions$(groupReservationId, { silent }),
      status: this.groupOrdersService.getStatus$(groupReservationId, { silent }),
    }).pipe(
      map(
        ({ participants, status }): GroupSnapshot => ({
          participants: participants.participants,
          status,
        }),
      ),
    );
  }

  updateDish(change: DishQuantityChange): void {
    if (this.groupStatus() !== 'Open' || this.submitting() || this.removing() ||
        this.updatingDish() || this.showSubmitModal() || this.guestToRemove()) return;
    this.updatingDish.set(true);
    this.stopPolling();
    this.refreshSubscription?.unsubscribe();
    this.mutationSubscription = this.groupOrdersService.updateDish$(
      this.groupReservationId(), change.participantId, change.dishId, change.quantity,
    ).pipe(
      finalize(() => this.updatingDish.set(false)),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe({
      next: (snapshot) => {
        this.participants.set(snapshot.participants);
        this.updatedAt.set(new Date());
        this.syncError.set(false);
        this.startPolling();
      },
      error: () => this.startPolling(),
    });
  }

  private applySnapshot(snapshot: GroupSnapshot): void {
    this.participants.set(snapshot.participants);
    this.groupStatus.set(snapshot.status.status);
    this.maxCapacity.set(snapshot.status.maxCapacity);
    this.updatedAt.set(new Date());
    this.syncError.set(false);

    if (snapshot.status.status !== 'Open') {
      this.stopPolling();
      this.guestToRemove.set(null);
    }
  }

  private getUpdatedSnapshot$(silent: boolean) {
    // This owner endpoint synchronizes the customer's Order into the owner session.
    // Read the carts only after that write has completed.
    return this.groupOrdersService.getOwnerSessionId$(this.reservationId, { silent }).pipe(
      switchMap((owner) => this.getSnapshot$(owner.groupReservationId, silent)),
    );
  }

  private refresh(silent: boolean): void {
    const groupReservationId = this.groupReservationId();
    if (!groupReservationId || this.refreshing() || this.submitting() || this.removing() || this.updatingDish() || this.groupStatus() !== 'Open') {
      return;
    }

    this.refreshing.set(true);
    this.refreshSubscription = this.getUpdatedSnapshot$(silent)
      .pipe(
        finalize(() => this.refreshing.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (snapshot) => this.applySnapshot(snapshot),
        error: () => this.syncError.set(true),
      });
  }

  private startPolling(): void {
    this.stopPolling();
    const groupReservationId = this.groupReservationId();
    if (!groupReservationId || this.groupStatus() !== 'Open') {
      return;
    }

    this.pollingSubscription = interval(15_000)
      .pipe(
        exhaustMap(() => {
          if (this.refreshing() || this.submitting() || this.removing() || this.updatingDish()) {
            return EMPTY;
          }

          this.refreshing.set(true);
          return this.getUpdatedSnapshot$(true).pipe(
            tap((snapshot) => this.applySnapshot(snapshot)),
            catchError(() => {
              this.syncError.set(true);
              return EMPTY;
            }),
            finalize(() => this.refreshing.set(false)),
          );
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe();
  }

  private stopPolling(): void {
    this.pollingSubscription?.unsubscribe();
    this.pollingSubscription = undefined;
  }
}
