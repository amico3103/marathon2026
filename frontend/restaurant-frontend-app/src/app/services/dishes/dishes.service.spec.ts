import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { DishesService } from './dishes.service';

describe('reservation menu requests', () => {
  beforeEach(() => TestBed.configureTestingModule({ providers: [
    provideHttpClient(), provideHttpClientTesting(),
    { provide: API_BASE_URL, useValue: 'https://api.example' },
  ] }));

  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('keeps the reservation filter with category and sort', () => {
    TestBed.inject(DishesService).getDishes('MainCourse', 'PriceAscending', undefined, 'reservation-1').subscribe();
    const request = TestBed.inject(HttpTestingController).expectOne(req => req.url === 'https://api.example/dishes');
    expect(request.request.params.get('reservationId')).toBe('reservation-1');
    expect(request.request.params.get('dishType')).toBe('MainCourse');
    expect(request.request.params.get('sortBy')).toBe('PriceAscending');
    request.flush([]);
  });

  it('leaves the ordinary menu unscoped', () => {
    TestBed.inject(DishesService).getDishes().subscribe();
    const request = TestBed.inject(HttpTestingController).expectOne('https://api.example/dishes');
    expect(request.request.params.has('reservationId')).toBeFalse();
    request.flush([]);
  });
});
