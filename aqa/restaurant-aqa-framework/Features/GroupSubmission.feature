@l2 @group-preorder
Feature: Sending a consolidated group pre-order to the restaurant

  Rule: The owner sends only confirmed choices to the restaurant

    @ui @L2-01
    Scenario: The owner's session belongs to the reviewed reservation
      Given a group containing only the owner
      When the owner opens the group review
      Then the owner has their own session in this group

    @ui @L2-02
    Scenario: Sending the group pre-order makes the review read-only
      Given a group with confirmed and unconfirmed carts
      And the owner is reviewing the group
      When the owner confirms the consolidated pre-order
      Then the pre-order is sent to the restaurant and cannot be edited

    @ui @L2-03
    Scenario: Identical dishes from the owner and guests are combined once
      Given a group where the owner and both guests have confirmed the same dish
      And the owner is reviewing the group
      When the owner confirms the consolidated pre-order
      Then the restaurant order contains four portions in one dish entry

    @ui @L2-04
    Scenario: Unconfirmed choices are excluded when sending the pre-order
      Given a group with confirmed and unconfirmed carts
      And the owner is reviewing the group
      When the owner confirms the consolidated pre-order
      Then the restaurant order contains only the confirmed portions

    @ui @L2-05
    Scenario: The assigned waiter sees the choices sent by the owner
      Given a group with confirmed and unconfirmed carts
      And the owner is reviewing the group
      And the owner confirms the consolidated pre-order
      When the assigned waiter opens the reservation pre-order
      Then the waiter sees the dish and quantity sent by the owner

  Rule: An invalid or unauthorized submission cannot change the group

    @ui @L2-06
    Scenario: A group with no confirmed carts cannot be sent
      Given a group with only unconfirmed choices
      When the owner opens the group review
      Then sending the pre-order is unavailable
      When the owner attempts to send the pre-order through the service
      Then sending the group is refused because "no carts are confirmed"
      And the group and order remain unchanged

    @api @negative @L2-07
    Scenario: A confirmed session containing no dishes cannot be sent
      Given an isolated group with a confirmed but empty cart
      When the owner attempts to send the pre-order through the service
      Then sending the group is refused because "confirmed carts contain no dishes"
      And the group and order remain unchanged

    @api @negative @L2-08
    Scenario: A guest's confirmed cart does not replace the missing owner order
      Given a group with a confirmed guest cart but no owner order
      When the owner attempts to send the pre-order through the service
      Then sending the group is refused because "the owner order is missing"
      And the group and order remain unchanged

    @api @negative @L2-09
    Scenario: A missing reservation cannot be submitted
      Given a group with confirmed and unconfirmed carts
      When the owner attempts to send the group for a reservation that does not exist
      Then sending the group is refused because "the reservation is missing"
      And the group and order remain unchanged

    @api @negative @L2-10
    Scenario: A missing group cannot be submitted
      Given a group with confirmed and unconfirmed carts
      When the owner attempts to send a group that does not exist
      Then sending the group is refused because "the group is missing"
      And the group and order remain unchanged

    @api @negative @L2-11
    Scenario: Sending the same pre-order again does not duplicate its dishes
      Given a group with confirmed and unconfirmed carts
      And the owner has already sent the consolidated pre-order
      When the owner attempts to send the pre-order through the service
      Then sending the group is refused because "the group was already submitted"
      And the group and order remain unchanged

    @api @negative @L2-12
    Scenario Outline: Only the owner can send the consolidated pre-order
      Given a group with confirmed and unconfirmed carts
      When the pre-order is submitted by "<requester>"
      Then sending the group is restricted to its owner
      And the group and order remain unchanged

      Examples: People who do not have owner access
        | requester         |
        | another customer  |
        | waiter            |
        | guest             |
        | anonymous visitor |
