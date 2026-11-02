import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CardReservationComponent } from './card-reservation.component';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { Router } from '@angular/router';
import { ReservationStatusCustomer } from '@app/models/reservations/reservation.model';

describe('CardReservationComponent', () => {
  let component: CardReservationComponent;
  let fixture: ComponentFixture<CardReservationComponent>;
  let router: jasmine.SpyObj<Router>;

  beforeEach(async () => {
    router = jasmine.createSpyObj<Router>('Router', ['navigate']);
    router.navigate.and.resolveTo(true);
    await TestBed.configureTestingModule({
      providers: [
        { provide: API_BASE_URL, useValue: 'https://test.api' },
        { provide: Router, useValue: router },
      ],
      imports: [CardReservationComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(CardReservationComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('r', {
      id: 'reservation-1',
      tableId: 'table-1',
      startTime: new Date('2026-10-29T18:00:00Z'),
      endTime: new Date('2026-10-29T20:00:00Z'),
      guestCount: 3,
      status: ReservationStatusCustomer.Reserved,
      restaurantName: 'Green & Tasty',
      locationAddress: 'Main Street 1',
      waiterId: 'waiter-1',
    });
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('opens the real group pre-order page for group reservations', () => {
    const button = [...fixture.nativeElement.querySelectorAll('button')].find(
      (candidate: HTMLButtonElement) => candidate.textContent?.trim() === 'Group pre-order',
    ) as HTMLButtonElement;
    button.click();

    expect(router.navigate).toHaveBeenCalledWith([
      '/reservations',
      'reservation-1',
      'group-order',
    ]);
  });
});
