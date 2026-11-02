import { HttpClient, HttpContext, HttpHeaders } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { AuthenticationService } from '@app/auth/services/authentication.service';
import { SKIP_ERROR_TOAST } from '@app/auth/skip-error-toast.context';
import {
  GroupReservationStatusResponse,
  OwnerGuestSessionResponse,
} from '@app/models/group-orders/group-reservation-status.model';
import { GroupSessionsResponse } from '@app/models/group-orders/group-sessions-response.model';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { Observable } from 'rxjs';

export interface GroupOrderRequestOptions {
  silent?: boolean;
}

@Injectable({ providedIn: 'root' })
export class GroupOrdersService {
  private readonly baseUrl = inject(API_BASE_URL);
  private readonly http = inject(HttpClient);
  private readonly authService = inject(AuthenticationService);

  getSessions$(
    groupReservationId: string,
    options: GroupOrderRequestOptions = {},
  ): Observable<GroupSessionsResponse> {
    return this.http.get<GroupSessionsResponse>(
      `${this.baseUrl}/guest/groups/${encodeURIComponent(groupReservationId)}/sessions`,
      this.ownerRequestOptions(options),
    );
  }

  getStatus$(
    groupReservationId: string,
    options: GroupOrderRequestOptions = {},
  ): Observable<GroupReservationStatusResponse> {
    return this.http.get<GroupReservationStatusResponse>(
      `${this.baseUrl}/guest/groups/${encodeURIComponent(groupReservationId)}/status`,
      this.ownerRequestOptions(options),
    );
  }

  getOwnerSessionId$(
    reservationId: string,
    options: GroupOrderRequestOptions = {},
  ): Observable<OwnerGuestSessionResponse> {
    return this.http.get<OwnerGuestSessionResponse>(
      `${this.baseUrl}/reservations/${encodeURIComponent(reservationId)}/guest-session-id`,
      { context: new HttpContext().set(SKIP_ERROR_TOAST, options.silent === true) },
    );
  }

  submit$(groupReservationId: string, reservationId: string): Observable<unknown> {
    return this.http.post(
      `${this.baseUrl}/group-reservations/${encodeURIComponent(groupReservationId)}/submit`,
      { reservationId },
    );
  }

  removeSession$(groupReservationId: string, guestSessionId: string): Observable<GroupSessionsResponse> {
    return this.http.delete<GroupSessionsResponse>(
      `${this.baseUrl}/guest/groups/${encodeURIComponent(groupReservationId)}/sessions/${encodeURIComponent(guestSessionId)}`,
      this.ownerRequestOptions({}),
    );
  }

  updateDish$(groupId: string, sessionId: string, dishId: string, quantity: number): Observable<GroupSessionsResponse> {
    return this.http.put<GroupSessionsResponse>(
      `${this.baseUrl}/guest/groups/${encodeURIComponent(groupId)}/sessions/${encodeURIComponent(sessionId)}`,
      { dishId, quantity }, this.ownerRequestOptions({}),
    );
  }

  private ownerRequestOptions(options: GroupOrderRequestOptions): {
    headers: HttpHeaders;
    context: HttpContext;
  } {
    const idToken = this.authService.getTokens()?.idToken;
    const headers = idToken
      ? new HttpHeaders({ Authorization: `Bearer ${idToken}` })
      : new HttpHeaders();

    return {
      headers,
      context: new HttpContext().set(SKIP_ERROR_TOAST, options.silent === true),
    };
  }
}
