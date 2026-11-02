import { Participant } from '@app/models/group-orders/participant.model';
import { consolidatePreOrder } from './consolidate-pre-order';

const participants: Participant[] = [
  {
    id: 'owner',
    type: 'Owner',
    name: 'Owner',
    isSubmitted: true,
    total: 20,
    items: [{ dish: { id: 'soup', name: 'Soup', price: 10 }, quantity: 2 }],
  },
  {
    id: 'guest-ready',
    type: 'Guest',
    name: 'Ready guest',
    isSubmitted: true,
    total: 10,
    items: [{ dish: { id: 'soup', name: 'Soup', price: 10 }, quantity: 1 }],
  },
  {
    id: 'guest-pending',
    type: 'Guest',
    name: 'Pending guest',
    isSubmitted: false,
    total: 999,
    items: [{ dish: { id: 'dessert', name: 'Dessert', price: 999 }, quantity: 1 }],
  },
];

describe('consolidatePreOrder', () => {
  it('combines matching dishes from submitted carts only', () => {
    expect(consolidatePreOrder(participants)).toEqual([
      {
        dish: { id: 'soup', name: 'Soup', price: 10 },
        quantity: 3,
        total: 30,
      },
    ]);
  });
});
