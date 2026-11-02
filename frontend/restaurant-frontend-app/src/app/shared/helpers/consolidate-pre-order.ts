import { DishShort, Participant } from '@app/models/group-orders/participant.model';

export interface ConsolidatedPreOrderItem {
  dish: DishShort;
  quantity: number;
  total: number;
}

export function consolidatePreOrder(participants: Participant[]): ConsolidatedPreOrderItem[] {
  const items = new Map<string, ConsolidatedPreOrderItem>();

  for (const participant of participants) {
    if (!participant.isSubmitted) {
      continue;
    }

    for (const item of participant.items) {
      const existing = items.get(item.dish.id);
      const quantity = (existing?.quantity ?? 0) + item.quantity;
      items.set(item.dish.id, {
        dish: item.dish,
        quantity,
        total: item.dish.price * quantity,
      });
    }
  }

  return [...items.values()];
}
