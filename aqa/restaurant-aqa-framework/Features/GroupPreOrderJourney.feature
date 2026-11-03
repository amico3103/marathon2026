@e2e @group-preorder
Feature: A group pre-order reaches the restaurant with only retained guests' dishes

  Rule: Removing a guest excludes their unique dish throughout the complete journey

    @ui @E2E-01 @trace-always
    Scenario: Two guests choose dishes and the waiter receives the order after one guest is removed
      Given a private group with an initial owner pre-order
      When two invited guests choose different dishes and confirm their carts
      And the owner opens the group review
      Then the owner sees both confirmed guest carts and the correct combined amounts
      When the owner removes the guest with the unique dish
      Then the retained participants and their amounts remain correct
      When the owner confirms the consolidated pre-order
      Then the pre-order is sent to the restaurant and cannot be edited
      When the assigned waiter opens the reservation pre-order
      Then the waiter sees the dish and quantity sent by the owner
      And the removed guest's unique dish is absent from the restaurant order
