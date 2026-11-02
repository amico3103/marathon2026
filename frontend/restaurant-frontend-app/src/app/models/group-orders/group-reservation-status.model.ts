export type GroupReservationStatus = 'Open' | 'Submitted' | 'Closed';

export interface GroupReservationStatusResponse {
  currentParticipants: number;
  maxCapacity: number;
  status: GroupReservationStatus;
}

export interface OwnerGuestSessionResponse {
  guestSessionId: string;
  groupReservationId: string;
}
