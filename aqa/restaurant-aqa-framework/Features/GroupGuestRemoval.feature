@l3 @group-preorder
Feature: Removing guests from a group pre-order

  Rule: The owner removes only the selected guest and their contribution

    @ui @L3-01
    Scenario: Removing one guest preserves the other participants and their choices
      Given a group with different dishes in its carts
      And the owner is reviewing the group
      When the owner removes the "confirmed guest" from the review
      Then only the selected guest disappears and all remaining totals are correct

    @ui @L3-02
    Scenario: A removed confirmed cart no longer contributes to the consolidated order
      Given a group with confirmed and unconfirmed carts
      And the owner is reviewing the group
      When the owner removes the "confirmed guest" from the review
      Then only the selected guest disappears and all remaining totals are correct

    @ui @L3-03
    Scenario: Removing an unconfirmed cart does not change the confirmed grand total
      Given a group with confirmed and unconfirmed carts
      And the owner is reviewing the group
      When the owner removes the "unconfirmed guest" from the review
      Then only the selected guest disappears and all remaining totals are correct

    @ui @L3-04
    Scenario: Removing the last invited guest leaves the owner's pre-order
      Given a group with the owner and one confirmed guest
      And the owner is reviewing the group
      When the owner removes the "confirmed guest" from the review
      Then only the selected guest disappears and all remaining totals are correct
      And only the owner's cart remains

    @api @L3-05
    Scenario: Removing the only guest before an owner session exists leaves an empty group
      Given a group with one confirmed guest and no owner session
      When the owner requests removal of the "confirmed guest"
      Then the guest removal leaves an empty and open group

    @ui @L3-09
    Scenario: Cancelling removal preserves the selected guest and all amounts
      Given a group with confirmed and unconfirmed carts
      And the owner is reviewing the group
      When the owner cancels removal of the confirmed guest
      Then the selected guest and all displayed amounts remain
      And the group and order remain unchanged

  Rule: Removal cannot cross ownership or group boundaries

    @api @negative @L3-06
    Scenario: A participant from a different group cannot be removed
      Given a group with confirmed and unconfirmed carts
      And another independent group with its own guests
      When the owner requests removal of the "foreign guest"
      Then removing the guest is refused because "the guest belongs to another group"
      And the group and order remain unchanged
      And the other group and order remain unchanged

    @api @negative @L3-07
    Scenario: A participant who does not exist cannot be removed
      Given a group with confirmed and unconfirmed carts
      When the owner requests removal of the "missing guest"
      Then removing the guest is refused because "the guest is missing"
      And the group and order remain unchanged

    @api @negative @L3-08
    Scenario Outline: People without owner access cannot remove a guest
      Given a group with confirmed and unconfirmed carts
      When removal of a confirmed guest is requested by "<requester>"
      Then removing the guest is restricted to its owner
      And the group and order remain unchanged

      Examples: People who cannot manage this group
        | requester         |
        | another customer  |
        | waiter            |
        | guest             |
        | anonymous visitor |

    @api @negative @L3-10
    Scenario: The owner's own session cannot be removed
      Given a group with confirmed and unconfirmed carts
      When the owner requests removal of the "owner"
      Then removing the guest is refused because "the owner must remain"
      And the group and order remain unchanged

    @ui @negative @L3-11
    Scenario Outline: A group that is no longer open cannot lose guests
      Given a group with confirmed and unconfirmed carts
      And the group is already "<state>"
      When the owner opens the group review
      Then guest removal is unavailable for the locked group
      When the owner requests removal of the "confirmed guest"
      Then removing the guest is refused because "the group is locked"
      And the group and order remain unchanged

      Examples: Locked states
        | state     |
        | submitted |
        | closed    |
