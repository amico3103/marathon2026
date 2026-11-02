import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { DishQuantityChange, Participant } from '@app/models/group-orders/participant.model';

@Component({
  standalone: true,
  selector: 'app-participant-card',
  templateUrl: './participant-card.component.html',
  styleUrl: './participant-card.component.scss',
  imports: [CurrencyPipe],
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class ParticipantCardComponent {
  readonly participant = input.required<Participant>();
  readonly isCurrentUser = input(false);
  readonly canRemove = input(false);
  readonly removeClick = output<Participant>();
  readonly canEdit = input(false);
  readonly editBusy = input(false);
  readonly quantityChange = output<DishQuantityChange>();

  changeQuantity(dishId: string, quantity: number): void {
    if (this.canEdit() && !this.editBusy() && quantity >= 0) {
      this.quantityChange.emit({ participantId: this.participant().id, dishId, quantity });
    }
  }

  readonly title = computed(() => {
    const { type, name } = this.participant();
    return this.isCurrentUser() ? 'My Pre-Order' : `${type === 'Owner' ? 'Owner' : 'Guest'}: ${name}`;
  });

  readonly statusLabel = computed(() =>
    this.participant().isSubmitted ? 'Submitted' : 'Still choosing',
  );
}
