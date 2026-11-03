@l1 @group-preorder
Feature: Reviewing group participants

  Rule: The owner sees the choices made by their invited guests

    @ui @L1-01
    Scenario: An invited guest's choices appear in the owner's review
      Given a customer with a private table
      When the owner books the table and invites a guest who chooses a dish
      Then the owner sees the invited guest and their chosen dish

    @ui @L1-02
    Scenario: Each participant's dishes and subtotal are accurate
      Given a group with different dishes in its carts
      When the owner opens the group review
      Then every participant has the correct dishes, quantities and subtotal

    @ui @L1-03
    Scenario: The owner sees a guest's changed choices without reloading
      Given a group containing only the owner
      And the owner is reviewing the group
      And a guest has joined and chosen a dish
      When the guest increases their dish quantity
      Then the owner sees the changed quantity without reloading

    @ui @L1-04
    Scenario: A guest's confirmed cart contributes to the grand total
      Given a group containing only the owner
      And the owner is reviewing the group
      And a guest has joined and chosen a dish
      When the guest confirms their cart
      Then the owner sees the guest cart confirmed and included in the grand total

    @ui @L1-05
    Scenario: An unconfirmed cart remains visible without increasing the grand total
      Given a group with confirmed and unconfirmed carts
      When the owner opens the group review
      Then the unconfirmed cart is visible but excluded from the grand total

    @ui @L1-06
    Scenario: A guest who has not chosen dishes remains in the group
      Given a group containing only the owner
      And a guest without dishes has joined the group
      When the owner opens the group review
      Then the guest without dishes remains visible with a zero subtotal

    @api @L1-07
    Scenario: A newly created group can have no participant sessions
      Given a group without any participant sessions
      When the participant list is requested by "owner"
      Then the group has no participants and a zero total

    @ui @L1-08
    Scenario: The owner can review a group before any guests join
      Given a group containing only the owner
      When the owner opens the group review
      Then only the owner's empty cart is displayed

  Rule: Participant information is available only to the reservation owner

    @api @negative @L1-09
    Scenario Outline: Other signed-in people cannot read the owner's participant list
      Given a group with confirmed and unconfirmed carts
      When the participant list is requested by "<requester>"
      Then the participant information is protected

      Examples: Authenticated people who do not own the reservation
        | requester        |
        | another customer |
        | waiter           |

    @api @negative @L1-10
    Scenario Outline: A guest invitation does not grant owner access
      Given a group with confirmed and unconfirmed carts
      When the participant list is requested by "<requester>"
      Then the participant information is protected

      Examples: People without an owner sign-in
        | requester         |
        | guest             |
        | anonymous visitor |

    @api @negative @L1-11
    Scenario: The owner is told when a requested group does not exist
      Given a group without any participant sessions
      When the owner requests participants of a group that does not exist
      Then the missing group is reported

    @ui @negative @L1-11
    Scenario: A missing group review provides a clear recovery option
      Given a group containing only the owner
      When the owner opens a group review that does not exist
      Then the owner sees that the group cannot be found and can retry
