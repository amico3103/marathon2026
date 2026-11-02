import { TestBed } from '@angular/core/testing';
import { Participant } from '@app/models/group-orders/participant.model';
import { ParticipantCardComponent } from './participant-card.component';

const OLENA: Participant = {
  id: 'olena',
  type: 'Guest',
  name: 'Olena',
  isSubmitted: false,
  total: 440,
  items: [
    {
      dish: {
        id: 'dish2',
        name: 'Tom Yum',
        price: 220,
        imageUrl: 'assets/img/dishes/spring-salad.jpg',
      },
      quantity: 2,
    },
  ],
};

const OWNER: Participant = {
  id: 'owner',
  type: 'Owner',
  name: 'Yevhenii',
  isSubmitted: true,
  total: 0,
  items: [],
};

function render(participant: Participant): HTMLElement {
  const fixture = TestBed.createComponent(ParticipantCardComponent);
  fixture.componentRef.setInput('participant', participant);
  fixture.detectChanges();
  return fixture.nativeElement as HTMLElement;
}

function renderCurrentUser(participant: Participant): HTMLElement {
  const fixture = TestBed.createComponent(ParticipantCardComponent);
  fixture.componentRef.setInput('participant', participant);
  fixture.componentRef.setInput('isCurrentUser', true);
  fixture.detectChanges();
  return fixture.nativeElement as HTMLElement;
}

describe('Level 1 participant card', () => {
  it('keeps quantity controls mounted and disabled while an edit is saving', () => {
    const fixture = TestBed.createComponent(ParticipantCardComponent);
    fixture.componentRef.setInput('participant', OLENA);
    fixture.componentRef.setInput('canEdit', true);
    const change = jasmine.createSpy('change');
    fixture.componentInstance.quantityChange.subscribe(change);
    fixture.detectChanges();
    const controls = fixture.nativeElement.querySelector('.dish-controls');
    fixture.componentRef.setInput('editBusy', true);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.dish-controls')).toBe(controls);
    for (const button of controls.querySelectorAll('button')) {
      expect(button.disabled).toBeTrue();
      button.click();
    }
    fixture.componentInstance.changeQuantity('dish2', 3);
    expect(change).not.toHaveBeenCalled();
    fixture.componentRef.setInput('editBusy', false);
    fixture.componentRef.setInput('participant', {
      ...OLENA, total: 660, items: [{ ...OLENA.items[0], quantity: 3 }],
    });
    fixture.detectChanges();
    expect(controls.querySelector('span').textContent.trim()).toBe('3');
    expect(fixture.nativeElement.textContent).toContain('$660.00');
  });
  it('emits absolute quantities and removal for an editable guest cart', () => {
    const fixture = TestBed.createComponent(ParticipantCardComponent);
    fixture.componentRef.setInput('participant', OLENA);
    fixture.componentRef.setInput('canEdit', true);
    const change = jasmine.createSpy('change');
    fixture.componentInstance.quantityChange.subscribe(change);
    fixture.detectChanges();
    fixture.nativeElement.querySelector('[aria-label="Increase Tom Yum"]').click();
    expect(change).toHaveBeenCalledWith({ participantId: 'olena', dishId: 'dish2', quantity: 3 });
    fixture.nativeElement.querySelector('[aria-label="Decrease Tom Yum"]').click();
    expect(change).toHaveBeenCalledWith({ participantId: 'olena', dishId: 'dish2', quantity: 1 });
    fixture.nativeElement.querySelector('[aria-label="Remove dish Tom Yum"]').click();
    expect(change).toHaveBeenCalledWith({ participantId: 'olena', dishId: 'dish2', quantity: 0 });
    fixture.componentRef.setInput('canEdit', false);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.dish-controls')).toBeNull();
  });
  it('offers accessible removal for guests and emits the selected participant', () => {
    const fixture = TestBed.createComponent(ParticipantCardComponent);
    fixture.componentRef.setInput('participant', OLENA);
    fixture.componentRef.setInput('canRemove', true);
    const remove = jasmine.createSpy('remove');
    fixture.componentInstance.removeClick.subscribe(remove);
    fixture.detectChanges();
    fixture.nativeElement.querySelector('button[aria-label="Remove Olena"]').click();
    expect(remove).toHaveBeenCalledWith(OLENA);
    fixture.componentRef.setInput('participant', OWNER);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.remove-button')).toBeNull();
    fixture.componentRef.setInput('participant', OLENA);
    fixture.componentRef.setInput('canRemove', false);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.remove-button')).toBeNull();
  });
  it('renders the guest title', () => {
    expect(render(OLENA).querySelector('h2')?.textContent).toContain('Guest: Olena');
  });

  it('renders the owner title', () => {
    expect(render(OWNER).querySelector('h2')?.textContent).toContain('Owner: Yevhenii');
  });

  it('labels the current owner session as My Pre-Order', () => {
    expect(renderCurrentUser(OWNER).querySelector('h2')?.textContent).toContain('My Pre-Order');
  });

  it('renders dish name, quantity and line price', () => {
    const text = render(OLENA).textContent ?? '';
    expect(text).toContain('Tom Yum');
    expect(text).toContain('2');
    expect(text).toContain('$440.00');
  });

  it('renders the participant subtotal', () => {
    expect(render(OLENA).textContent).toContain('Subtotal');
  });

  it('renders an empty basket state', () => {
    expect(render(OWNER).textContent).toContain('No dishes yet');
  });

  it('reserves a 40 by 40 image area', () => {
    const image = render(OLENA).querySelector('img');
    expect(image?.getAttribute('width')).toBe('40');
    expect(image?.getAttribute('height')).toBe('40');
  });

  it('renders the pending basket status as text', () => {
    expect(render(OLENA).textContent).toContain('Still choosing');
  });

  it('renders the submitted basket status as text', () => {
    expect(render({ ...OLENA, isSubmitted: true }).textContent).toContain('Submitted');
  });
});
